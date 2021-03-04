using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqlServerBatchExtensions
    {
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IMethodCallTranslatorPlugin, MathTranslation>();
            SearchConditionBooleanGuard.AddTypeField(typeof(SearchConditionConvertingExpressionVisitor), "_isSearchCondition");
#if EFCORE50
            services.Replace(ServiceDescriptor.Singleton<IRelationalParameterBasedSqlProcessorFactory, XysParameterBasedSqlProcessorFactory>());
#elif EFCORE31
            SearchConditionBooleanGuard.AddTypeField(typeof(NullSemanticsRewritingExpressionVisitor), "_canOptimize");
#endif
        }

        public static SqlServerDbContextOptionsBuilder UseBulk(this SqlServerDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new RelationalBatchDbContextOptionsExtension<
                EnhancedQuerySqlGeneratorFactory,
                SqlServerQuerySqlGeneratorFactory,
                SqlServerBatchOperationProvider>("SqlServerBatchExtension", ConfigureServices);
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }
}
