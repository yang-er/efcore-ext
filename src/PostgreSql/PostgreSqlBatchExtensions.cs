using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace Microsoft.EntityFrameworkCore
{
    public static class PostgreSqlBatchExtensions
    {
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IMethodCallTranslatorPlugin, DateTimeOffsetTranslation>();
            services.AddSingleton<IMemberTranslatorPlugin, DateTimeOffsetTranslation>();
#if EFCORE50
            services.Replace(ServiceDescriptor.Singleton<IRelationalParameterBasedSqlProcessorFactory, XysParameterBasedSqlProcessorFactory>());
#endif
        }

        public static NpgsqlDbContextOptionsBuilder UseBulk(this NpgsqlDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new RelationalBatchDbContextOptionsExtension<
                EnhancedQuerySqlGeneratorFactory,
                NpgsqlQuerySqlGeneratorFactory,
                PostgreSqlBatchOperationProvider>("PostgreSqlBatchExtension", ConfigureServices);
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }
}