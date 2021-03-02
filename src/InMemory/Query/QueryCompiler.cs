using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc />
    public class InMemoryBulkQueryCompiler : BulkQueryCompiler
    {
        private readonly IBulkQueryCompilationContextFactory _qccFactory;

        /// <summary>
        /// Instantiates the <see cref="InMemoryBulkQueryCompiler"/>.
        /// </summary>
        public InMemoryBulkQueryCompiler(
            IBulkQueryCompilationContextFactory qccFactory,
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
            _qccFactory = qccFactory;
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
                        var queryingEnumerable = TranslateQueryingEnumerable(root);
                        return CompileDeleteCore<TResult>(queryingEnumerable, rootType);


                    case nameof(BatchOperationExtensions.BatchUpdate)
                    when genericMethod == BatchOperationMethods.BatchUpdate:
                        EnsureQueryExpression(root);
                        var updateSelector = methodCallExpression.Arguments[1].UnwrapLambdaFromQuote();
                        ReshapeForUpdate(model, ref updateSelector, updateSelector.Parameters[0], out var updateShaper);
                        queryingEnumerable = TranslateQueryingEnumerable(ApplySelect(root, updateSelector));
                        return CompileUpdateCore<TResult>(queryingEnumerable, updateShaper.Body.Type, updateShaper);


                    case nameof(BatchOperationExtensions.BatchInsertInto)
                    when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                        queryingEnumerable = TranslateQueryingEnumerable(root);
                        return CompileSelectIntoCore<TResult>(queryingEnumerable, rootType);


                    case nameof(BatchOperationExtensions.BatchUpdateJoin)
                    when genericMethod == BatchOperationMethods.BatchUpdateJoin:
                        // The second parameter is update shaper.
                        var newBatchJoin = VisitorHelper.RemapBatchUpdateJoin(methodCallExpression, out var outerMember, out _);
                        updateSelector = newBatchJoin.Arguments[1].UnwrapLambdaFromQuote();
                        ReshapeForUpdate(model, ref updateSelector, updateSelector.Parameters[0].MakeMemberAccess(outerMember), out updateShaper);
                        queryingEnumerable = TranslateQueryingEnumerable(ApplySelect(newBatchJoin.Arguments[0], updateSelector));
                        return CompileUpdateCore<TResult>(queryingEnumerable, updateShaper.Body.Type, updateShaper);


                    case nameof(BatchOperationExtensions.Upsert)
                    when genericMethod == BatchOperationMethods.UpsertCollapsed:
                        if (!TryReshapeForUpsert(model.FindEntityType(rootType), methodCallExpression, out root, out var insertShaper, out var updateExtractor)) goto default;
                        queryingEnumerable = TranslateQueryingEnumerable(root);
                        return CompileUpsertCore<TResult>(queryingEnumerable, rootType, methodCallExpression.Method.GetGenericArguments()[1], insertShaper, updateExtractor);


                    default:
                        throw new InvalidOperationException(
                            CoreStrings.TranslationFailed(
                                methodCallExpression.Print()));
                }
            }

            return base.CompileQueryCore<TResult>(database, query, model, async);

            static Expression ApplySelect(Expression innerQuery, LambdaExpression selector)
                => Expression.Call(
                    QueryableMethods.Select.MakeGenericMethod(selector.Parameters[0].Type, selector.Body.Type),
                    innerQuery,
                    Expression.Quote(selector));

            InvocationExpression TranslateQueryingEnumerable(Expression innerQuery)
                => Expression.Invoke(
                    Expression.Constant(
                        _qccFactory.CreateQueryExecutor<object>(
                            async,
                            innerQuery)),
                    QueryCompilationContext.QueryContextParameter);
        }

        protected virtual Func<QueryContext, TResult> CompileDeleteCore<TResult>(InvocationExpression queryingEnumerable, Type rootType)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(DeleteOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable),
                    typeof(TResult) == typeof(Task<int>)
                        ? BulkQueryExecutorExecuteAsync
                        : BulkQueryExecutorExecute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        protected virtual void ReshapeForUpdate(IModel model, ref LambdaExpression selector, Expression origin, out LambdaExpression updateShaper)
        {
            var remapperType = typeof(UpdateRemapper<>).MakeGenericType(origin.Type);
            var entityType = model.FindEntityType(origin.Type);
            var shaperArgs = Expression.Parameter(remapperType, "shaper");
            var shaperOrigin = Expression.Property(shaperArgs, "Origin");
            var shaperUpdate = Expression.Property(shaperArgs, "Update");

            var sentences = new List<Expression>();
            ReshapeForUpdate(selector.Body, shaperOrigin, shaperUpdate, entityType, sentences, (another, _) => another);
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
        }

        static void ReshapeForUpdate(Expression body, Expression current, Expression another, IEntityType entityType, List<Expression> sentences, Func<Expression, Expression, Expression> resultSelector)
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
                    sentences.Add(Expression.Assign(member, resultSelector(anotherMember, assignment.Expression)));
                    continue;
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
                    ReshapeForUpdate(assignment.Expression, member, anotherMember, navigation.ForeignKey.DeclaringEntityType, sentences, resultSelector);
                }
            }
        }

        protected virtual Func<QueryContext, TResult> CompileUpdateCore<TResult>(InvocationExpression queryingEnumerable, Type rootType, LambdaExpression updateShaper)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(UpdateOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable,
                        Expression.Constant(updateShaper.Compile())),
                    typeof(TResult) == typeof(Task<int>)
                        ? BulkQueryExecutorExecuteAsync
                        : BulkQueryExecutorExecute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        protected virtual Func<QueryContext, TResult> CompileSelectIntoCore<TResult>(InvocationExpression queryingEnumerable, Type rootType)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(SelectIntoOperation<>).MakeGenericType(rootType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable),
                    typeof(TResult) == typeof(Task<int>)
                        ? BulkQueryExecutorExecuteAsync
                        : BulkQueryExecutorExecute),
                QueryCompilationContext.QueryContextParameter)
                .Compile();
        }

        protected virtual bool TryReshapeForUpsert(IEntityType entityType, MethodCallExpression upsert, out Expression reshaped, out Expression insertShaper, out Expression updateExtractor)
        {
            var outerExpression = upsert.Arguments[0];
            var innerExpression = upsert.Arguments[1];
            var insertExpression = upsert.Arguments[2].UnwrapLambdaFromQuote();
            var updateExpression = upsert.Arguments[3].UnwrapLambdaFromQuote();

            if (insertExpression.Body is not MemberInitExpression insertInit
                || insertInit.NewExpression.Arguments.Count != 0
                || !entityType.TryGuessKey(insertInit.Bindings, out var key))
            {
                reshaped = insertShaper = updateExtractor = null;
                return false;
            }

            var outerAndInner = upsert.Method.GetGenericArguments();
            var outerType = outerAndInner[0];
            var innerType = outerAndInner[1];
            var outerEnumerableType = typeof(IEnumerable<>).MakeGenericType(outerType);
            var groupJoinResultType = TransparentIdentifierFactory.Create(innerType, outerEnumerableType);
            var groupJoinInnerMemberInfo = groupJoinResultType.GetField("Outer");
            var groupJoinOuterEnumerableMemberInfo = groupJoinResultType.GetField("Inner");
            var leftJoinResultType = typeof(MergeRemapper<,>).MakeGenericType(outerType, innerType);
            var leftJoinInnerMemberInfo = leftJoinResultType.GetProperty("Inner");
            var leftJoinOuterMemberInfo = leftJoinResultType.GetProperty("Outer");
            var updateExtractorType = typeof(Func<,,,>).MakeGenericType(typeof(QueryContext), outerType, outerType, outerType);

            var innerParameter = Expression.Parameter(innerType, "source");
            var outerEnumerableParameter = Expression.Parameter(outerEnumerableType, "targets");
            var groupJoinResultParameter = Expression.Parameter(groupJoinResultType, "ti");
            var outerParameter = Expression.Parameter(outerType, "target");

            GenericUtility.CreateJoinKey(
                Expression.Parameter(outerType, "target"), key,
                insertExpression.Parameters[0], insertInit.Bindings,
                out var joinKeyType,
                out var targetKeySelector,
                out var sourceKeySelector);

            var defaultIfEmptyWithoutArgument = typeof(Enumerable).GetMethods()
                .Single(mi => mi.Name == nameof(Enumerable.DefaultIfEmpty) && mi.GetParameters().Length == 1);

            if (updateExpression.Body is ConstantExpression constantExpression
                && constantExpression.Value == null)
            {
                updateExtractor = Expression.Constant(null, updateExtractorType);
            }
            else
            {
                var newUpdateBody = QueryContextParameterVisitor.Process(updateExpression.Body, updateExpression.Parameters);
                var sentences = new List<Expression>();
                ReshapeForUpdate(newUpdateBody, updateExpression.Parameters[0], updateExpression.Parameters[0], entityType, sentences, (_, expr) => expr);
                sentences.Add(updateExpression.Parameters[0]);

                updateExtractor =
                    Expression.Constant(
                        Expression.Lambda(
                            updateExtractorType,
                            Expression.Block(sentences),
                            QueryCompilationContext.QueryContextParameter,
                            updateExpression.Parameters[0],
                            updateExpression.Parameters[1])
                        .Compile());
            }

            reshaped =
                Expression.Call(
                    QueryableMethods.SelectManyWithCollectionSelector.MakeGenericMethod(groupJoinResultType, outerType, leftJoinResultType),
                    Expression.Call(
                        QueryableMethods.GroupJoin.MakeGenericMethod(innerType, outerType, joinKeyType, groupJoinResultType),
                        innerExpression,
                        outerExpression,
                        Expression.Quote(sourceKeySelector),
                        Expression.Quote(targetKeySelector),
                        Expression.Quote(
                            Expression.Lambda(
                                Expression.New(
                                    groupJoinResultType.GetConstructors()[0],
                                    new[] { innerParameter, outerEnumerableParameter },
                                    new[] { groupJoinInnerMemberInfo, groupJoinOuterEnumerableMemberInfo }),
                                innerParameter,
                                outerEnumerableParameter))),
                    Expression.Quote(
                        Expression.Lambda(
                            Expression.Call(
                                defaultIfEmptyWithoutArgument.MakeGenericMethod(outerType),
                                Expression.Field(groupJoinResultParameter, groupJoinOuterEnumerableMemberInfo)),
                            groupJoinResultParameter)),
                    Expression.Quote(
                        Expression.Lambda(
                            Expression.New(
                                leftJoinResultType.GetConstructors()[0],
                                new Expression[]
                                {
                                    outerParameter,
                                    Expression.Field(groupJoinResultParameter, groupJoinInnerMemberInfo),
                                },
                                new[] { leftJoinOuterMemberInfo, leftJoinInnerMemberInfo }),
                            groupJoinResultParameter,
                            outerParameter)));

            insertShaper =
                Expression.Constant(
                    Expression.Lambda(
                        typeof(Func<,,>).MakeGenericType(typeof(QueryContext), innerType, outerType),
                        QueryContextParameterVisitor.Process(insertInit, insertExpression.Parameters),
                        QueryCompilationContext.QueryContextParameter,
                        insertExpression.Parameters[0])
                    .Compile());

            return true;
        }

        protected virtual Func<QueryContext, TResult> CompileUpsertCore<TResult>(InvocationExpression queryingEnumerable, Type targetType, Type sourceType, Expression insertShaper, Expression updateExtractor)
        {
            return Expression.Lambda<Func<QueryContext, TResult>>(
                Expression.Call(
                    Expression.New(
                        typeof(UpsertOperation<,>).MakeGenericType(targetType, sourceType).GetConstructors()[0],
                        QueryCompilationContext.QueryContextParameter,
                        queryingEnumerable,
                        insertShaper,
                        updateExtractor),
                    typeof(TResult) == typeof(Task<int>)
                        ? BulkQueryExecutorExecuteAsync
                        : BulkQueryExecutorExecute),
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
