using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal class RelationalBatchDbContextOptionsExtension<TNewFactory, TOldFactory, TProvider> :
        IDbContextOptionsExtension
        where TOldFactory : class, IQuerySqlGeneratorFactory
        where TNewFactory : class, IEnhancedQuerySqlGeneratorFactory<TOldFactory>
        where TProvider : RelationalBatchOperationProvider
    {
        private DbContextOptionsExtensionInfo _info;
        private readonly string _name;
        private readonly Action<IServiceCollection> _configureServices;

        public RelationalBatchDbContextOptionsExtension(
            string name,
            Action<IServiceCollection> configureServices = null)
        {
            _name = name;
            _configureServices = configureServices ?? (_ => { });
        }

        public DbContextOptionsExtensionInfo Info =>
            _info ??= new BatchDbContextOptionsExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            var sd = services.FirstOrDefault(d => d.ServiceType == typeof(IQuerySqlGeneratorFactory));
            if (sd?.ImplementationType != typeof(TOldFactory))
                throw new InvalidOperationException("No such IQuerySqlGeneratorFactory.");
            services[services.IndexOf(sd)] = ServiceDescriptor.Singleton(sd.ServiceType, typeof(TNewFactory));

            services.AddSingleton<IBatchOperationProvider, TProvider>();
            services.AddSingleton<IAnonymousExpressionFactory, AnonymousExpressionFactory>();
            services.Replace(ServiceDescriptor.Scoped<IQueryCompiler, BulkQueryCompiler>());
            services.Replace(ServiceDescriptor.Singleton<IShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>());
            services.Replace(ServiceDescriptor.Singleton<IQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>());
            services.Replace(ServiceDescriptor.Singleton<IQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>());
            _configureServices.Invoke(services);
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private class BatchDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
        {
            private readonly string _name;

            public BatchDbContextOptionsExtensionInfo(
                RelationalBatchDbContextOptionsExtension<TNewFactory, TOldFactory, TProvider> extension) : base(extension)
            {
                _name = extension._name;
                LogFragment = $"using {_name} ";
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment { get; }

            public override long GetServiceProviderHashCode() => 0;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo[_name] = "1";
        }
    }
}
