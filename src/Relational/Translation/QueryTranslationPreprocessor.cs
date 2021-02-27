using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
#if EFCORE31
    using RelationalQueryCompilationContext = QueryCompilationContext;
#endif

    public class XysQueryTranslationPreprocessor : RelationalQueryTranslationPreprocessor
    {
        private readonly RelationalQueryCompilationContext _queryCompilationContext;

        public XysQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            RelationalQueryTranslationPreprocessorDependencies relationalDependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, relationalDependencies, queryCompilationContext)
        {
            _queryCompilationContext = (RelationalQueryCompilationContext)queryCompilationContext;
        }

#if EFCORE31

        public override Expression Process(Expression query)
        {
            query = new EnumerableToQueryableMethodConvertingExpressionVisitor().Visit(query);
            query = new QueryMetadataExtractingExpressionVisitor(_queryCompilationContext).Visit(query);
            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = new AllAnyToContainsRewritingExpressionVisitor().Visit(query);
            query = new GroupJoinFlatteningExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new EntityEqualityRewritingExpressionVisitor(_queryCompilationContext).Rewrite(query);
            query = new SubqueryMemberPushdownExpressionVisitor().Visit(query);
            query = new XysNavigationExpandingExpressionVisitor(_queryCompilationContext, Dependencies.EvaluatableExpressionFilter)
                .Expand(query);
            query = new FunctionPreprocessingExpressionVisitor().Visit(query);
            // new EnumerableVerifyingExpressionVisitor().Visit(query);

            return query;
        }

#elif EFCORE50

        public override Expression Process(Expression query)
        {
            Check.NotNull(query, nameof(query));

            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = NormalizeQueryableMethod(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new SubqueryMemberPushdownExpressionVisitor(QueryCompilationContext.Model).Visit(query);
            query = new XysNavigationExpandingExpressionVisitor(this, QueryCompilationContext, Dependencies.EvaluatableExpressionFilter)
                .Expand(query);
            query = new QueryOptimizingExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);

            return _queryCompilationContext.QuerySplittingBehavior == QuerySplittingBehavior.SplitQuery
                ? new SplitIncludeRewritingExpressionVisitor().Visit(query)
                : query;
        }

#endif

    }

    public class XysNavigationExpandingExpressionVisitor : NavigationExpandingExpressionVisitor
    {

#if EFCORE31

        public XysNavigationExpandingExpressionVisitor(
            QueryCompilationContext queryCompilationContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter)
            : base(queryCompilationContext, evaluatableExpressionFilter)
        {
        }

#elif EFCORE50

        public XysNavigationExpandingExpressionVisitor(
            QueryTranslationPreprocessor queryTranslationPreprocessor,
            QueryCompilationContext queryCompilationContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter)
            : base(queryTranslationPreprocessor, queryCompilationContext, evaluatableExpressionFilter)
        {
        }

#endif

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                Expression result = null;

                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                        when genericMethod == BatchOperationMethods.CreateCommonTable &&
                             methodCallExpression.Arguments[1] is ParameterExpression parameter:

                        result = VisitorHelper.CreateDirect(
                            methodCallExpression,
                            Expression.Call(
                                parameter,
                                parameter.Type.GetMethod("get_Item"),
                                Expression.Constant(0))); ;
                        break;
                }

                if (result == null)
                {
                    throw new InvalidOperationException(
                        CoreStrings.QueryFailed(methodCallExpression.Print(), GetType().Name));
                }
                else
                {
                    return result;
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        public override Expression Expand(Expression query)
        {
            if (query is not MethodCallExpression methodCallExpression)
            {
                return base.Expand(query);
            }

            var method = methodCallExpression.Method;
            if (method.DeclaringType != typeof(BatchOperationExtensions))
            {
                return base.Expand(query);
            }

            var genericMethod = method.GetGenericMethodDefinition();
            var genericArguments = method.GetGenericArguments();
            switch (method.Name)
            {
                case nameof(BatchOperationExtensions.CreateCommonTable)
                when genericMethod == BatchOperationMethods.CreateCommonTable:
                    return methodCallExpression;


                case nameof(BatchOperationExtensions.BatchDelete)
                when genericMethod == BatchOperationMethods.BatchDelete:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                case nameof(BatchOperationExtensions.BatchUpdate)
                when genericMethod == BatchOperationMethods.BatchUpdate:
                    return BatchUpdateExpand(
                        Expression.Call(
                            QueryableMethods.Select.MakeGenericMethod(genericArguments[0], genericArguments[0]),
                            methodCallExpression.Arguments));


                case nameof(BatchOperationExtensions.BatchInsertInto)
                when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                case nameof(BatchOperationExtensions.BatchUpdateJoin)
                when genericMethod == BatchOperationMethods.BatchUpdateJoin:
                    return BatchUpdateExpand(
                        RemapBatchUpdateJoin(methodCallExpression));


                default:
                    throw TranslateFailed();
            }

            Exception TranslateFailed()
                => new InvalidOperationException(
                    CoreStrings.QueryFailed(query.Print(), GetType().Name));

            Expression BatchUpdateExpand(Expression toUpdate)
            {
                var expanded = base.Expand(toUpdate);

                // TODO: Is type hierarchy affected?
                if (expanded is not MethodCallExpression fakeSelect ||
                    fakeSelect.Method.GetGenericMethodDefinition() != QueryableMethods.Select)
                    throw TranslateFailed();

                var newSelectTypes = fakeSelect.Method.GetGenericArguments();
                return Expression.Call(
                    BatchOperationMethods.BatchUpdateExpanded.MakeGenericMethod(newSelectTypes),
                    fakeSelect.Arguments[0],
                    Expression.Quote(fakeSelect.Arguments[1].UnwrapLambdaFromQuote()));
            }
        }

        private static MethodCallExpression RemapBatchUpdateJoin(MethodCallExpression methodCallExpression)
        {
            var genericArguments = methodCallExpression.Method.GetGenericArguments();
            var outerType = genericArguments[0];
            var innerType = genericArguments[1];
            var joinKeyType = genericArguments[2];

            var transparentIdentifierType = TransparentIdentifierFactory.Create(outerType, innerType);
            var transparentIdentifierOuterMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Outer");
            var transparentIdentifierInnerMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Inner");
            var transparentIdentifierParameter = Expression.Parameter(transparentIdentifierType, "tree");
            var transparentIdentifierReplacement = new[]
            {
                Expression.Field(transparentIdentifierParameter, transparentIdentifierOuterMemberInfo),
                Expression.Field(transparentIdentifierParameter, transparentIdentifierInnerMemberInfo),
            };

            var outerParameter = Expression.Parameter(outerType, "outer");
            var innerParameter = Expression.Parameter(innerType, "inner");
            var predicate = methodCallExpression.Arguments[5].UnwrapLambdaFromQuote();
            var selector = methodCallExpression.Arguments[4].UnwrapLambdaFromQuote();

            return Expression.Call(
                QueryableMethods.Select.MakeGenericMethod(transparentIdentifierType, outerType),
                Expression.Call(
                    QueryableMethods.Where.MakeGenericMethod(transparentIdentifierType),
                    Expression.Call(
                        QueryableMethods.Join.MakeGenericMethod(outerType, innerType, joinKeyType, transparentIdentifierType),
                        methodCallExpression.Arguments[0],
                        methodCallExpression.Arguments[1],
                        methodCallExpression.Arguments[2],
                        methodCallExpression.Arguments[3],
                        Expression.Quote(
                            Expression.Lambda(
                                Expression.New(
                                    transparentIdentifierType.GetConstructors().Single(),
                                    new[] { outerParameter, innerParameter },
                                    new[] { transparentIdentifierOuterMemberInfo, transparentIdentifierInnerMemberInfo }),
                                outerParameter,
                                innerParameter))),
                    Expression.Quote(
                        Expression.Lambda(
                            new ReplacingExpressionVisitor(
                                predicate.Parameters.ToArray(),
                                transparentIdentifierReplacement).Visit(predicate.Body),
                            transparentIdentifierParameter))),
                Expression.Quote(
                    Expression.Lambda(
                        new ReplacingExpressionVisitor(
                            selector.Parameters.ToArray(),
                            transparentIdentifierReplacement).Visit(selector.Body),
                        transparentIdentifierParameter)));
        }
    }

    public class XysQueryTranslationPreprocessorFactory :
        IQueryTranslationPreprocessorFactory,
        IServiceAnnotation<IQueryTranslationPreprocessorFactory, RelationalQueryTranslationPreprocessorFactory>
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;
        private readonly RelationalQueryTranslationPreprocessorDependencies _relationalDependencies;

        public XysQueryTranslationPreprocessorFactory(
            QueryTranslationPreprocessorDependencies dependencies,
            RelationalQueryTranslationPreprocessorDependencies relationalDependencies)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            return new XysQueryTranslationPreprocessor(_dependencies, _relationalDependencies, queryCompilationContext);
        }

        private static readonly Type _parentPreprocessorType = typeof(RelationalQueryTranslationPreprocessorFactory);

        public static void TryReplace(IServiceCollection services)
        {
            var factory = services
                .Where(s => s.ServiceType == typeof(IQueryTranslationPreprocessorFactory))
                .ToList();

            if (factory.Count != 1 || factory[0].ImplementationType != _parentPreprocessorType)
                throw new InvalidOperationException($"Implementation of IQueryTranslationPreprocessorFactory is not supported.");

            services.Replace(ServiceDescriptor.Singleton<IQueryTranslationPreprocessorFactory, XysQueryTranslationPreprocessorFactory>());
        }
    }
}
