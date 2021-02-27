using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;
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
            builder1.AddOrUpdateExtension(new InMemoryBatchDbContextOptionsExtension());
            return builder;
        }

        internal class InMemoryBatchDbContextOptionsExtension : IDbContextOptionsExtension
        {
            private DbContextOptionsExtensionInfo _info;

            public DbContextOptionsExtensionInfo Info =>
                _info ??= new BatchDbContextOptionsExtensionInfo(this);

            public void ApplyServices(IServiceCollection services)
            {
                services.AddSingleton<IBatchOperationProvider, InMemoryBatchOperationProvider>();
                services.Replace(ServiceDescriptor.Scoped<IQueryCompiler, ExplicitQueryCompiler>());
                services.Replace(ServiceDescriptor.Singleton<IQueryTranslationPreprocessorFactory, XysQueryTranslationPreprocessorFactory>());
                services.Replace(ServiceDescriptor.Singleton<IQueryableMethodTranslatingExpressionVisitorFactory, XysQueryableMethodTranslatingExpressionVisitorFactory>());
            }

            public void Validate(IDbContextOptions options)
            {
            }

            private class BatchDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
            {
                public BatchDbContextOptionsExtensionInfo(
                    InMemoryBatchDbContextOptionsExtension extension) :
                    base(extension)
                {
                }

                public override bool IsDatabaseProvider => false;

                public override string LogFragment => string.Empty;

                public override long GetServiceProviderHashCode() => 0;

                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
            }
        }
    }
}
