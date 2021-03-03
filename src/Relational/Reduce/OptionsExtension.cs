using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class TableSplittingJoinsRemovalDbContextOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo _info;

        public DbContextOptionsExtensionInfo Info =>
            _info ??= new TableSplittingJoinsRemovalDbContextOptionsExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IQueryTranslationPostprocessorFactory));
            if (descriptor == null) throw new InvalidOperationException("No IQueryTranslationPostprocessorFactory registered.");

            services.Replace(ServiceDescriptor.Singleton(
                typeof(IQueryTranslationPostprocessorFactory),
                typeof(TableSplittingJoinsWrappingQueryTranslationPostprocessorFactory<>).MakeGenericType(descriptor.ImplementationType)));
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private class TableSplittingJoinsRemovalDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
        {
            public TableSplittingJoinsRemovalDbContextOptionsExtensionInfo(
                TableSplittingJoinsRemovalDbContextOptionsExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => "using TableSplittingJoinsRemoval ";

            public override long GetServiceProviderHashCode() => 0;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo["TableSplittingJoinsRemoval"] = "1";
        }
    }
}
