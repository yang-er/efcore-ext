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
using static Microsoft.EntityFrameworkCore.Bulk.BatchOperationMethods;

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
                    when genericMethod == s_BatchDelete_TSource:
                        EnsureQueryExpression(root);
                        return CompileDeleteCore<TResult>(TranslateQueryingEnumerable(root, rootType), rootType);

                    case nameof(BatchOperationExtensions.BatchUpdate)
                    when genericMethod == s_BatchUpdate_TSource:
                        EnsureQueryExpression(root);
                        return CompileUpdateCore<TResult>(TranslateQueryingEnumerable(root, rootType), rootType, TranslateUpdateShaper(UnquoteLambda(methodCallExpression.Arguments[1])));
                }
            }

            return base.CompileQueryCore<TResult>(database, query, model, async);

            InvocationExpression TranslateQueryingEnumerable(Expression innerQuery, Type sourceEntityType)
            {
                var innerEnumerableType = async
                    ? typeof(IAsyncEnumerable<>).MakeGenericType(sourceEntityType)
                    : typeof(IEnumerable<>).MakeGenericType(sourceEntityType);

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

            LambdaExpression UnquoteLambda(Expression expression)
            {
                return (expression as UnaryExpression)?.Operand as LambdaExpression;
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

        private static LambdaExpression TranslateUpdateShaper(LambdaExpression expression)
        {
            var param = expression.Parameters[0];
            NewExpression newExpression;
            IEnumerable<MemberBinding> bindings;
            var sentences = new List<Expression>();

            switch (expression.Body)
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
                        $"Type of {expression.Body.NodeType} is not supported in InMemory update yet.");
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

                var member = Expression.MakeMemberAccess(param, assignment.Member);
                var result = InMemoryParameterAccessVisitor.Process(assignment.Expression);
                sentences.Add(Expression.Assign(member, result));
            }

            return Expression.Lambda(
                Expression.Block(typeof(void), sentences),
                QueryCompilationContext.QueryContextParameter,
                param);
        }

        private Func<QueryContext, TResult> CompileUpdateCore<TResult>(InvocationExpression queryingEnumerable, Type rootType, LambdaExpression updateShaper)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(UpdateOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable,
                        Expression.Constant(updateShaper.Compile())),
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
