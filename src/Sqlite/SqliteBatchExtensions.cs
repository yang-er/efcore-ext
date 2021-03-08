using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Sqlite.Query;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqliteBatchExtensions
    {
        public static SqliteDbContextOptionsBuilder UseBulk(this SqliteDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new SqliteBatchOptionsExtension();
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }

    public class SqliteBatchOptionsExtension : RelationalBatchOptionsExtension
    {
        public override string Name => "SqliteBatchExtension";

        protected override void ApplyServices(BatchServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, SqliteBulkQueryableMethodTranslatingExpressionVisitorFactory>();

            services.TryAdd<IBulkQuerySqlGeneratorFactory, SqliteBulkQuerySqlGeneratorFactory>();
            services.TryAdd<IQueryCompiler, SqliteBulkQueryCompiler>();
#if EFCORE50
            services.TryAdd<IRelationalBulkParameterBasedSqlProcessorFactory, RelationalBulkParameterBasedSqlProcessorFactory>();
            services.TryAdd<IBulkQueryCompilationContextFactory, SqliteBulkQueryCompilationContextFactory>();
#elif EFCORE31
            services.TryAdd<IBulkQueryCompilationContextFactory, BulkQueryCompilationContextFactory>();
            SearchConditionBooleanGuard.AddTypeField(typeof(NullSemanticsRewritingExpressionVisitor), "_canOptimize");
#endif
        }
    }
}
