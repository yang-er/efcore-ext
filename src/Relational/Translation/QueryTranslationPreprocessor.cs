using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static Microsoft.EntityFrameworkCore.Bulk.BatchOperationMethods;

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
                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                        when genericMethod == s_CreateCommonTable_TSource_TTarget
                          && methodCallExpression.Arguments[1] is ParameterExpression param:
                        return NavigationExpansionExpressionFactory(
                            methodCallExpression,
                            Expression.Call(
                                param,
                                param.Type.GetMethod("get_Item"),
                                Expression.Constant(0)));
                }

                throw new InvalidOperationException(CoreStrings.QueryFailed(methodCallExpression.Print(), GetType().Name));
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        private static Expression<Func<Expression, Expression, Expression>> CreateNavigationExpansion()
        {
            var origin = Expression.Parameter(typeof(Expression), "origin");
            var entity = Expression.Parameter(typeof(Expression), "entity");
            var treeExp = typeof(NavigationExpandingExpressionVisitor).GetNestedType("NavigationTreeExpression", BindingFlags.NonPublic);
            var navExp = typeof(NavigationExpandingExpressionVisitor).GetNestedType("NavigationExpansionExpression", BindingFlags.NonPublic);

            var node = Expression.Variable(treeExp, "node");
            var treeLeaf = Expression.New(treeExp.GetConstructors()[0], entity);
            var nodeAssign = Expression.Assign(node, treeLeaf);

            var exp = Expression.New(navExp.GetConstructors()[0], origin, node, node, Expression.Constant("cte", typeof(string)));
            var block = Expression.Block(new[] { node }, nodeAssign, exp);
            return Expression.Lambda<Func<Expression, Expression, Expression>>(block, origin, entity);
        }

        private static readonly Func<Expression, Expression, Expression> NavigationExpansionExpressionFactory
            = CreateNavigationExpansion().Compile();
    }

    public class XysQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
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
