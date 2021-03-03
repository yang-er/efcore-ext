using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor : QueryTranslationPostprocessor
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly QueryCompilationContext _queryCompilationContext;
        private readonly QueryTranslationPostprocessor _queryTranslationPostprocessor;

        public TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor(
            QueryTranslationPostprocessorDependencies dependencies,
            QueryTranslationPostprocessor queryTranslationPostprocessor,
            ISqlExpressionFactory sqlExpressionFactory,
            QueryCompilationContext queryCompilationContext)
#if EFCORE50
            : base(dependencies, queryCompilationContext)
#elif EFCORE31
            : base(dependencies)
#endif
        {
            _queryTranslationPostprocessor = queryTranslationPostprocessor;
            _queryCompilationContext = queryCompilationContext;
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        public override Expression Process(Expression query)
        {
            query = new SelfJoinsPruningExpressionVisitor(_queryCompilationContext, _sqlExpressionFactory).Reduce(query);
            query = _queryTranslationPostprocessor.Process(query);
            return query;
        }
    }

    public class TableSplittingJoinsWrappingQueryTranslationPostprocessorFactory<TPostprocessorFactory> :
        IQueryTranslationPostprocessorFactory
        where TPostprocessorFactory : IQueryTranslationPostprocessorFactory
    {
        private readonly QueryTranslationPostprocessorDependencies _dependencies;
        private readonly RelationalQueryTranslationPostprocessorDependencies _relationalDependencies;
        private readonly IQueryTranslationPostprocessorFactory _factory;

        public TableSplittingJoinsWrappingQueryTranslationPostprocessorFactory(
            QueryTranslationPostprocessorDependencies dependencies,
            RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
            IServiceProvider serviceProvider)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
            _factory = ActivatorUtilities.CreateInstance<TPostprocessorFactory>(serviceProvider);
        }

        public virtual QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
            => new TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor(
                _dependencies,
                _factory.Create(queryCompilationContext),
                _relationalDependencies.SqlExpressionFactory,
                queryCompilationContext);
    }
}
