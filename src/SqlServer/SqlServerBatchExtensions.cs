using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

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

        public static SqlServerDbContextOptionsBuilder UseMathExtensions(this SqlServerDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new SqlServerMathExtension();
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }

    public class SqlServerMathExtension : DbContextOptionsExtension
    {
        public override string Name => "SqlServerMathExtensions";

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
            services.TryAdd<IMethodCallTranslatorPlugin, MathTranslationPlugin>();
        }
    }

    public class SqlServerBatchOptionsExtension : RelationalBatchOptionsExtension
    {
        public override string Name => "SqlServerBatchExtension";

        protected override void ApplyServices(ExtensionServicesBuilder services)
        {
            services.TryAdd<IAnonymousExpressionFactory, AnonymousExpressionFactory>();

            services.TryAdd<IBulkShapedQueryCompilingExpressionVisitorFactory, RelationalBulkShapedQueryCompilingExpressionVisitorFactory>();
            services.TryAdd<IBulkQueryTranslationPreprocessorFactory, RelationalBulkQueryTranslationPreprocessorFactory>();
            services.TryAdd<IBulkQueryTranslationPostprocessorFactory, BypassBulkQueryTranslationPostprocessorFactory>();
#if EFCORE31 || EFCORE50
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory>();
#elif EFCORE60
            services.TryAdd<IBulkQueryableMethodTranslatingExpressionVisitorFactory, SqlServerBulkQueryableMethodTranslatingExpressionVisitorFactory>();
#endif

            services.TryAdd<IBulkQuerySqlGeneratorFactory, SqlServerBulkQuerySqlGeneratorFactory>();
            services.TryAdd<IQueryCompiler, SqlServerBulkQueryCompiler>();
#if EFCORE50 || EFCORE60
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
