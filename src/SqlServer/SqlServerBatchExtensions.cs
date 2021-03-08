using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqlServerBatchExtensions
    {
        public static SqlServerDbContextOptionsBuilder UseBulk(this SqlServerDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new SqlServerBatchOptionsExtension();
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }

    public class SqlServerBatchOptionsExtension : RelationalBatchOptionsExtension
    {
        public override string Name => "SqlServerBatchExtension";

        protected override void ApplyServices(BatchServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>();

            services.TryAdd<IMethodCallTranslatorPlugin, MathTranslationPlugin>();
            services.TryAdd<IBulkQuerySqlGeneratorFactory, SqlServerBulkQuerySqlGeneratorFactory>();
            services.TryAdd<IQueryCompiler, SqlServerBulkQueryCompiler>();
#if EFCORE50
            services.TryAdd<IRelationalBulkParameterBasedSqlProcessorFactory, RelationalBulkParameterBasedSqlProcessorFactory>();
            services.TryAdd<IBulkQueryCompilationContextFactory, SqlServerBulkQueryCompilationContextFactory>();
#elif EFCORE31
            services.TryAdd<IBulkQueryCompilationContextFactory, BulkQueryCompilationContextFactory>();
            SearchConditionBooleanGuard.AddTypeField(typeof(NullSemanticsRewritingExpressionVisitor), "_canOptimize");
#endif
            SearchConditionBooleanGuard.AddTypeField(typeof(SearchConditionConvertingExpressionVisitor), "_isSearchCondition");
        }
    }
}
