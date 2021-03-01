namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc cref="IQueryableMethodTranslatingExpressionVisitorFactory" />
    public interface IBulkQueryableMethodTranslatingExpressionVisitorFactory : IQueryableMethodTranslatingExpressionVisitorFactory
    {
    }

    /// <inheritdoc cref="IQueryTranslationPostprocessorFactory" />
    public interface IBulkQueryTranslationPostprocessorFactory : IQueryTranslationPostprocessorFactory
    {
    }

    /// <inheritdoc cref="IQueryTranslationPreprocessorFactory" />
    public interface IBulkQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
    {
    }

    /// <inheritdoc cref="IShapedQueryCompilingExpressionVisitorFactory" />
    public interface IBulkShapedQueryCompilingExpressionVisitorFactory : IShapedQueryCompilingExpressionVisitorFactory
    {
    }

    /// <inheritdoc cref="IQueryTranslationPostprocessorFactory" />
    public class BypassBulkQueryTranslationPostprocessorFactory : IBulkQueryTranslationPostprocessorFactory
    {
        private readonly IQueryTranslationPostprocessorFactory _factory;

        /// <summary>Bypass the factory.</summary>
        public BypassBulkQueryTranslationPostprocessorFactory(IQueryTranslationPostprocessorFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            return _factory.Create(queryCompilationContext);
        }
    }

    /// <inheritdoc cref="IShapedQueryCompilingExpressionVisitorFactory" />
    public class BypassBulkShapedQueryCompilingExpressionVisitorFactory : IBulkShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly IShapedQueryCompilingExpressionVisitorFactory _factory;

        /// <summary>Bypass the factory.</summary>
        public BypassBulkShapedQueryCompilingExpressionVisitorFactory(IShapedQueryCompilingExpressionVisitorFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return _factory.Create(queryCompilationContext);
        }
    }
}
