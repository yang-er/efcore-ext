using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class XysQueryTranslationPreprocessor : QueryTranslationPreprocessor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        public XysQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
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

            return query;
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
            switch (method.Name)
            {
                case nameof(BatchOperationExtensions.CreateCommonTable)
                when genericMethod == BatchOperationMethods.CreateCommonTable:
                    return methodCallExpression;

                default:
                    throw new InvalidOperationException(
                        CoreStrings.QueryFailed(query.Print(), GetType().Name));
            }
        }
    }

    public class XysQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;

        public XysQueryTranslationPreprocessorFactory(QueryTranslationPreprocessorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));
            _dependencies = dependencies;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            return new XysQueryTranslationPreprocessor(_dependencies, queryCompilationContext);
        }
    }
}
