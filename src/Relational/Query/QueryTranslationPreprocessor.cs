using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Linq.Expressions;

#if EFCORE31
using RelationalQueryCompilationContext = Microsoft.EntityFrameworkCore.Query.QueryCompilationContext;
#pragma warning disable IDE0001
#pragma warning disable IDE0004
#endif

namespace Microsoft.EntityFrameworkCore.Query
{
    public class RelationalBulkQueryTranslationPreprocessor : RelationalQueryTranslationPreprocessor
    {
        private readonly RelationalQueryCompilationContext _queryCompilationContext;

        public RelationalBulkQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            RelationalQueryTranslationPreprocessorDependencies relationalDependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, relationalDependencies, queryCompilationContext)
        {
            _queryCompilationContext = (RelationalQueryCompilationContext)queryCompilationContext;
        }

        public override Expression Process(Expression query)
        {
            Check.NotNull(query, nameof(query));

#if EFCORE31
            query = new EnumerableToQueryableMethodConvertingExpressionVisitor().Visit(query);
            query = new QueryMetadataExtractingExpressionVisitor(_queryCompilationContext).Visit(query);
            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = new AllAnyToContainsRewritingExpressionVisitor().Visit(query);
            query = new GroupJoinFlatteningExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new EntityEqualityRewritingExpressionVisitor(_queryCompilationContext).Rewrite(query);
            query = new SubqueryMemberPushdownExpressionVisitor().Visit(query);
            query = new SupportCommonTableNavigationExpandingExpressionVisitor(_queryCompilationContext, Dependencies.EvaluatableExpressionFilter).Expand(query);
            query = new FunctionPreprocessingExpressionVisitor().Visit(query);
            // new EnumerableVerifyingExpressionVisitor().Visit(query);
#elif EFCORE50
            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = NormalizeQueryableMethod(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new SubqueryMemberPushdownExpressionVisitor(QueryCompilationContext.Model).Visit(query);
            query = new SupportCommonTableNavigationExpandingExpressionVisitor(this, QueryCompilationContext, Dependencies.EvaluatableExpressionFilter).Expand(query);
            query = new QueryOptimizingExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);

            if (_queryCompilationContext.QuerySplittingBehavior == QuerySplittingBehavior.SplitQuery)
                query = new SplitIncludeRewritingExpressionVisitor().Visit(query);
#endif

            return query;
        }
    }

    public class RelationalBulkQueryTranslationPreprocessorFactory :
        IBulkQueryTranslationPreprocessorFactory,
        IServiceAnnotation<IQueryTranslationPreprocessorFactory, RelationalQueryTranslationPreprocessorFactory>
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;
        private readonly RelationalQueryTranslationPreprocessorDependencies _relationalDependencies;

        public RelationalBulkQueryTranslationPreprocessorFactory(
            QueryTranslationPreprocessorDependencies dependencies,
            RelationalQueryTranslationPreprocessorDependencies relationalDependencies)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));

            return new RelationalBulkQueryTranslationPreprocessor(
                _dependencies,
                _relationalDependencies,
                queryCompilationContext);
        }
    }
}
