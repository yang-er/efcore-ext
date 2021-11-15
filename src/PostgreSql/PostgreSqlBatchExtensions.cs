using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query;
using System;
using System.Collections.Generic;

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

        public static NpgsqlDbContextOptionsBuilder UseLegacyDateTimeOffset(this NpgsqlDbContextOptionsBuilder builder)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new NpgsqlLegacyDateTimeOffsetExtension();
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }

    public class NpgsqlLegacyDateTimeOffsetExtension : DbContextOptionsExtension
    {
        public override string Name => "NpgsqlLegacyDateTimeOffset";

        internal override Dictionary<Type, ServiceLifetime> GetServiceLifetimes()
        {
            return new Dictionary<Type, ServiceLifetime>
            {
#if EFCORE31 || EFCORE50
                { typeof(IMethodCallTranslatorPlugin), ServiceLifetime.Singleton },
                { typeof(IMemberTranslatorPlugin), ServiceLifetime.Singleton },
#elif EFCORE60
                { typeof(IMethodCallTranslatorPlugin), ServiceLifetime.Scoped },
                { typeof(IMemberTranslatorPlugin), ServiceLifetime.Scoped },
#endif
            };
        }

        protected override void ApplyServices(ExtensionServicesBuilder services)
        {
            services.TryAdd<IMethodCallTranslatorPlugin, DateTimeOffsetTranslationPlugin>();
            services.TryAdd<IMemberTranslatorPlugin, DateTimeOffsetTranslationPlugin>();
        }
    }

    public class NpgsqlBatchOptionsExtension : RelationalBatchOptionsExtension
    {
        public override string Name => "PostgreSqlBatchExtension";

        protected override void ApplyServices(ExtensionServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>();

            services.TryAdd<IBulkQuerySqlGeneratorFactory, NpgsqlBulkQuerySqlGeneratorFactory>();
            services.TryAdd<IQueryCompiler, NpgsqlBulkQueryCompiler>();
#if EFCORE50 || EFCORE60
            services.TryAdd<IRelationalBulkParameterBasedSqlProcessorFactory, RelationalBulkParameterBasedSqlProcessorFactory>();
            services.TryAdd<IBulkQueryCompilationContextFactory, NpgsqlBulkQueryCompilationContextFactory>();
#elif EFCORE31
            services.TryAdd<IBulkQueryCompilationContextFactory, BulkQueryCompilationContextFactory>();
            SearchConditionBooleanGuard.AddTypeField(typeof(NullSemanticsRewritingExpressionVisitor), "_canOptimize");
#endif
        }
    }
}
