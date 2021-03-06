using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public abstract class RelationalBatchOptionsExtension : BatchOptionsExtension
    {
        protected override void ApplyServices(BatchServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>();
        }

        internal override HashSet<Type> GetRequiredServices()
        {
            var set = base.GetRequiredServices();
            set.Add(typeof(IBulkQuerySqlGeneratorFactory));
#if EFCORE50
            set.Add(typeof(IRelationalBulkParameterBasedSqlProcessorFactory));
#endif
            return set;
        }

        internal override Dictionary<Type, ServiceLifetime> GetServiceLifetimes()
        {
            var dict = base.GetServiceLifetimes();
            dict.Add(typeof(IMethodCallTranslatorPlugin), ServiceLifetime.Singleton);
            dict.Add(typeof(IMemberTranslatorPlugin), ServiceLifetime.Singleton);
            dict.Add(typeof(IBulkQuerySqlGeneratorFactory), ServiceLifetime.Singleton);
            dict.Add(typeof(IAnonymousExpressionFactory), ServiceLifetime.Singleton);
#if EFCORE50
            dict.Add(typeof(IRelationalBulkParameterBasedSqlProcessorFactory), ServiceLifetime.Singleton);
#endif
            return dict;
        }
    }
}
