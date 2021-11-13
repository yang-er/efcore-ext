using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class BulkQueryTranslationPreprocessor : QueryTranslationPreprocessor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        public BulkQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
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
#elif EFCORE50
            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = NormalizeQueryableMethod(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new SubqueryMemberPushdownExpressionVisitor(QueryCompilationContext.Model).Visit(query);
            query = new SupportCommonTableNavigationExpandingExpressionVisitor(this, QueryCompilationContext, Dependencies.EvaluatableExpressionFilter).Expand(query);
            query = new QueryOptimizingExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
#elif EFCORE60
            query = new InvocationExpressionRemovingExpressionVisitor().Visit(query);
            query = NormalizeQueryableMethod(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
            query = new SubqueryMemberPushdownExpressionVisitor(QueryCompilationContext.Model).Visit(query);
            query = new SupportCommonTableNavigationExpandingExpressionVisitor(this, Dependencies.NavigationExpansionExtensibilityHelper, QueryCompilationContext, Dependencies.EvaluatableExpressionFilter).Expand(query);
            query = new QueryOptimizingExpressionVisitor().Visit(query);
            query = new NullCheckRemovingExpressionVisitor().Visit(query);
#endif

            return query;
        }
    }

    public class BulkQueryTranslationPreprocessorFactory :
        IBulkQueryTranslationPreprocessorFactory,
        IServiceAnnotation<IQueryTranslationPreprocessorFactory, QueryTranslationPreprocessorFactory>
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;

        public BulkQueryTranslationPreprocessorFactory(QueryTranslationPreprocessorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));
            _dependencies = dependencies;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            return new BulkQueryTranslationPreprocessor(_dependencies, queryCompilationContext);
        }
    }
}
