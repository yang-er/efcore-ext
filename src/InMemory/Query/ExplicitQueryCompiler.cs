using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc />
    public class ExplicitQueryCompiler : BulkQueryCompiler
    {
        private static readonly MethodInfo database_CompileQuery
            = typeof(IDatabase).GetMethod(nameof(IDatabase.CompileQuery));

        private static readonly MethodInfo bulkQueryExecutor_Execute
            = typeof(IBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.Execute));

        private static readonly MethodInfo bulkQueryExecutor_ExecuteAsync
            = typeof(IBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.ExecuteAsync));

        public ExplicitQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDatabase database,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            IModel model)
            : base(queryContextFactory,
                  compiledQueryCache,
                  compiledQueryCacheKeyGenerator,
                  database,
                  logger,
                  currentContext,
                  evaluatableExpressionFilter,
                  model)
        {
        }

        public override Func<QueryContext, TResult> CompileQueryCore<TResult>(
            IDatabase database,
            Expression query,
            IModel model,
            bool async)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = methodCallExpression.Method.GetGenericMethodDefinition();
                var root = methodCallExpression.Arguments[0];
                var rootType = methodCallExpression.Method.GetGenericArguments()[0];

                switch (genericMethod.Name)
                {
                    case nameof(BatchOperationExtensions.BatchDelete)
                    when genericMethod == BatchOperationMethods.BatchDelete:
                        EnsureQueryExpression(root);
                        var queryingEnumerable = TranslateQueryingEnumerable(root, rootType);
                        return CompileDeleteCore<TResult>(queryingEnumerable, rootType);


                    case nameof(BatchOperationExtensions.BatchUpdate)
                    when genericMethod == BatchOperationMethods.BatchUpdate:
                        EnsureQueryExpression(root);
                        var updateSelector = methodCallExpression.Arguments[1].UnwrapLambdaFromQuote();
                        ReshapeForUpdate(model, ref updateSelector, updateSelector.Parameters[0], out var updateShaper);
                        queryingEnumerable = TranslateQueryingEnumerable(ApplySelect(root, updateSelector), updateSelector.Body.Type);
                        return CompileUpdateCore<TResult>(queryingEnumerable, updateSelector.Body.Type, updateShaper.Body.Type, updateShaper);


                    case nameof(BatchOperationExtensions.BatchInsertInto)
                    when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                        queryingEnumerable = TranslateQueryingEnumerable(root, rootType);
                        return CompileSelectIntoCore<TResult>(queryingEnumerable, rootType);


                    case nameof(BatchOperationExtensions.BatchUpdateJoin)
                    when genericMethod == BatchOperationMethods.BatchUpdateJoin:
                        // The second parameter is update shaper.
                        var newBatchJoin = VisitorHelper.RemapBatchUpdateJoin(methodCallExpression, out var outer, out _);
                        updateSelector = newBatchJoin.Arguments[1].UnwrapLambdaFromQuote();
                        ReshapeForUpdate(model, ref updateSelector, updateSelector.Parameters[0].MakeMemberAccess(outer), out updateShaper);
                        queryingEnumerable = TranslateQueryingEnumerable(ApplySelect(newBatchJoin.Arguments[0], updateSelector), updateSelector.Body.Type);
                        return CompileUpdateCore<TResult>(queryingEnumerable, updateSelector.Body.Type, updateShaper.Body.Type, updateShaper);
                }
            }

            return base.CompileQueryCore<TResult>(database, query, model, async);

            static Expression ApplySelect(Expression innerQuery, LambdaExpression selector)
            {
                return Expression.Call(
                    QueryableMethods.Select.MakeGenericMethod(selector.Parameters[0].Type, selector.Body.Type),
                    innerQuery,
                    Expression.Quote(selector));
            }

            InvocationExpression TranslateQueryingEnumerable(Expression innerQuery, Type entityType)
            {
                var innerEnumerableType = async
                    ? typeof(IAsyncEnumerable<>).MakeGenericType(entityType)
                    : typeof(IEnumerable<>).MakeGenericType(entityType);

                Delegate queryExecutor;

                try
                {
                    queryExecutor = (Delegate)database_CompileQuery
                        .MakeGenericMethod(innerEnumerableType)
                        .Invoke(database, new object[] { innerQuery, async });
                }
                catch (TargetInvocationException ex)
                {
                    throw ex?.InnerException ?? ex;
                }

                return Expression.Invoke(
                    Expression.Constant(queryExecutor),
                    QueryCompilationContext.QueryContextParameter);
            }
        }

        private Func<QueryContext, TResult> CompileDeleteCore<TResult>(InvocationExpression queryingEnumerable, Type rootType)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(DeleteOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable),
                    typeof(TResult) == typeof(Task<int>)
                        ? bulkQueryExecutor_ExecuteAsync
                        : bulkQueryExecutor_Execute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        private class UpdateRemapper<TEntity>
        {
            public UpdateRemapper(TEntity origin, TEntity update)
            {
                Origin = origin;
                Update = update;
            }

            public TEntity Origin { get; }

            public TEntity Update { get; }
        }

        protected virtual void ReshapeForUpdate(IModel model, ref LambdaExpression selector, Expression origin, out LambdaExpression updateShaper)
        {
            var remapperType = typeof(UpdateRemapper<>).MakeGenericType(origin.Type);
            var entityType = model.FindEntityType(origin.Type);
            var shaperArgs = Expression.Parameter(remapperType, "shaper");
            var shaperOrigin = Expression.Property(shaperArgs, "Origin");
            var shaperUpdate = Expression.Property(shaperArgs, "Update");

            var sentences = new List<Expression>();
            ReshapeForUpdate(selector.Body, shaperOrigin, shaperUpdate, entityType, sentences);
            sentences.Add(shaperOrigin);

            updateShaper = Expression.Lambda(
                Expression.Block(sentences),
                QueryCompilationContext.QueryContextParameter,
                shaperArgs);

            selector = Expression.Lambda(
                Expression.New(
                    remapperType.GetConstructors()[0],
                    new[] { origin, selector.Body },
                    remapperType.GetProperties()),
                selector.Parameters);

            static void ReshapeForUpdate(Expression body, Expression current, Expression another, IEntityType entityType, List<Expression> sentences)
            {
                NewExpression newExpression;
                IEnumerable<MemberBinding> bindings;

                switch (body)
                {
                    case MemberInitExpression memberInit:
                        newExpression = memberInit.NewExpression;
                        bindings = memberInit.Bindings;
                        break;

                    case NewExpression @new:
                        newExpression = @new;
                        bindings = Enumerable.Empty<MemberBinding>();
                        break;

                    default:
                        throw new NotImplementedException(
                            $"Type of {body.NodeType} is not supported in InMemory update yet.");
                }

                if (newExpression.Constructor.GetParameters().Length > 0)
                {
                    throw new InvalidOperationException("Non-simple constructor is not supported.");
                }

                foreach (var item in bindings)
                {
                    if (item is not MemberAssignment assignment)
                    {
                        throw new InvalidOperationException("Non-assignment binding is not supported.");
                    }

                    var member = Expression.MakeMemberAccess(current, assignment.Member);
                    var anotherMember = Expression.MakeMemberAccess(another, assignment.Member);
                    var memberInfo = entityType.FindProperty(assignment.Member);
                    if (memberInfo != null)
                    {
                        sentences.Add(Expression.Assign(member, anotherMember));
                        return;
                    }

                    var navigation = entityType.FindNavigation(assignment.Member);
                    if (navigation == null)
                    {
                        throw new NotSupportedException(
                            $"Unknown member \"{assignment.Member}\" is not supported in InMemory update yet.");
                    }
                    else if (!navigation.ForeignKey.IsOwnership)
                    {
                        throw new NotSupportedException(
                            $"Wrong member \"{navigation.ForeignKey}\". Only owned-navigation member can be updated.");
                    }
                    else
                    {
                        ReshapeForUpdate(assignment.Expression, member, anotherMember, navigation.ForeignKey.DeclaringEntityType, sentences);
                    }
                }
            }
        }

        private Func<QueryContext, TResult> CompileUpdateCore<TResult>(InvocationExpression queryingEnumerable, Type sourceType, Type rootType, LambdaExpression updateShaper)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(UpdateOperation<,>).MakeGenericType(sourceType, rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable,
                        Expression.Constant(updateShaper.Compile())),
                    typeof(TResult) == typeof(Task<int>)
                        ? bulkQueryExecutor_ExecuteAsync
                        : bulkQueryExecutor_Execute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        private Func<QueryContext, TResult> CompileSelectIntoCore<TResult>(InvocationExpression queryingEnumerable, Type rootType)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(SelectIntoOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable),
                    typeof(TResult) == typeof(Task<int>)
                        ? bulkQueryExecutor_ExecuteAsync
                        : bulkQueryExecutor_Execute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        private static void EnsureQueryExpression(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Call)
            {
                // expression = Call someFunc(IQueryable<T> some, ..., ...)
                var methodCall = (MethodCallExpression)expression;
                expression = methodCall.Arguments[0];

                if (methodCall.Method.DeclaringType == typeof(Queryable))
                {
                    switch (methodCall.Method.Name)
                    {
                        case nameof(Queryable.Take):
                        case nameof(Queryable.TakeWhile):
                        case nameof(Queryable.Skip):
                        case nameof(Queryable.SkipWhile):
                        case nameof(Queryable.OrderBy):
                        case nameof(Queryable.OrderByDescending):
                        case nameof(Queryable.Reverse):
                        case nameof(Queryable.ThenBy):
                        case nameof(Queryable.ThenByDescending):
                        case "TakeLast":
                        case "SkipLast":

                            throw new InvalidOperationException("Batch update/delete doesn't support take, skip, order.");

                        case nameof(Queryable.Select)
                        when methodCall.Arguments[1] is LambdaExpression lambda
                          && lambda.Body != lambda.Parameters[0]:
                            throw new InvalidOperationException("Batch update/delete doesn't support reshaping.");

                        case nameof(Queryable.GroupBy):
                        case nameof(Queryable.Join):
                        case nameof(Queryable.GroupJoin):
                        case nameof(Queryable.Except):
                        case nameof(Queryable.Intersect):
                        case nameof(Queryable.SelectMany):
                        case nameof(Queryable.Union):
                            throw new InvalidOperationException("Batch update/delete doesn't support set operations.");
                    }
                }
            }

#if EFCORE31
            bool validated = expression.NodeType == ExpressionType.Constant
                && expression.Type.IsConstructedGenericType
                && expression.Type.GetGenericTypeDefinition() == typeof(EntityQueryable<>);
#elif EFCORE50
            bool validated = expression is QueryRootExpression;
#endif

            if (!validated)
            {
                throw new ArgumentException("Query invalid. The operation entity must be in DbContext.");
            }
        }
    }
}
