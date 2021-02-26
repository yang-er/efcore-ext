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

                        result = VisitorHelper.CreateCommonTable(
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


                case nameof(BatchOperationExtensions.BatchDelete)
                when genericMethod == BatchOperationMethods.BatchDelete:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                case nameof(BatchOperationExtensions.BatchUpdate)
                when genericMethod == BatchOperationMethods.BatchUpdate:
                    var coreType = method.GetGenericArguments()[0];
                    var expanded = base.Expand(
                        Expression.Call(
                            QueryableMethods.Select.MakeGenericMethod(coreType, coreType),
                            methodCallExpression.Arguments));

                    // TODO: Is type hierarchy affected?
                    if (expanded is not MethodCallExpression fakeSelect ||
                        fakeSelect.Method.GetGenericMethodDefinition() != QueryableMethods.Select)
                        goto default;

                    var newSelectTypes = fakeSelect.Method.GetGenericArguments();
                    var updateBody = fakeSelect.Arguments[1].UnwrapLambdaFromQuote();
                    return Expression.Call(
                        BatchOperationMethods.BatchUpdateExpanded.MakeGenericMethod(newSelectTypes),
                        Expression.Call(
                            QueryableMethods.Select.MakeGenericMethod(newSelectTypes[0], newSelectTypes[0]),
                            fakeSelect.Arguments[0],
                            Expression.Lambda(updateBody.Parameters[0], updateBody.Parameters[0])),
                        Expression.Quote(updateBody));


                case nameof(BatchOperationExtensions.BatchInsertInto)
                when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                default:
                    throw new InvalidOperationException(
                        CoreStrings.QueryFailed(methodCallExpression.Print(), GetType().Name));
            }
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
