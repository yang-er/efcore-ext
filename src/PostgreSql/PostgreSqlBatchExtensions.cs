using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query;

namespace Microsoft.EntityFrameworkCore
{
    public static class PostgreSqlBatchExtensions
    {
        public static NpgsqlDbContextOptionsBuilder UseBulk(this NpgsqlDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new NpgsqlBatchOptionsExtension();
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }

    public class NpgsqlBatchOptionsExtension : RelationalBatchOptionsExtension
    {
        public override string Name => "PostgreSqlBatchExtension";

        protected override void ApplyServices(BatchServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>();

            services.TryAdd<IMethodCallTranslatorPlugin, DateTimeOffsetTranslationPlugin>();
            services.TryAdd<IMemberTranslatorPlugin, DateTimeOffsetTranslationPlugin>();
            services.TryAdd<IBulkQuerySqlGeneratorFactory, NpgsqlBulkQuerySqlGeneratorFactory>();
            services.TryAdd<IQueryCompiler, NpgsqlBulkQueryCompiler>();
#if EFCORE50
            services.TryAdd<IRelationalBulkParameterBasedSqlProcessorFactory, RelationalBulkParameterBasedSqlProcessorFactory>();
            services.TryAdd<IBulkQueryCompilationContextFactory, NpgsqlBulkQueryCompilationContextFactory>();
#elif EFCORE31
            services.TryAdd<IBulkQueryCompilationContextFactory, BulkQueryCompilationContextFactory>();
            SearchConditionBooleanGuard.AddTypeField(typeof(NullSemanticsRewritingExpressionVisitor), "_canOptimize");
#endif
        }
    }
}
