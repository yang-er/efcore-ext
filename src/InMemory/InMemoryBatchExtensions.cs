using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore
{
    public static class InMemoryBatchExtensions
    {
        public static InMemoryDbContextOptionsBuilder UseBulk(this InMemoryDbContextOptionsBuilder builder)
        {
            var builder1 = typeof(InMemoryDbContextOptionsBuilder)
                .GetProperty("OptionsBuilder", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(builder) as IDbContextOptionsBuilderInfrastructure;
            builder1.AddOrUpdateExtension(new InMemoryBatchOptionsExtension());
            return builder;
        }
    }

    internal class InMemoryBatchOptionsExtension : BatchOptionsExtension
    {
        public override string Name => "InMemoryBatchExtension";

        public override bool Relational => false;

        internal override void ApplyServices(BatchServicesBuilder services)
        {
            services.TryAdd<IBatchOperationProvider, InMemoryBatchOperationProvider>();

            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, BulkInMemoryQueryableMethodTranslatingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, BulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, BypassBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryCompilationContextFactory, BulkQueryCompilationContextFactory>();

            services.TryAdd<IQueryCompiler, InMemoryBulkQueryCompiler>();
        }
    }
}
