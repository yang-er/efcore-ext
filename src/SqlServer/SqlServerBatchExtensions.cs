using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqlServerBatchExtensions
    {
        public static SqlServerDbContextOptionsBuilder UseBulk(this SqlServerDbContextOptionsBuilder builder)
        {
            var builder1 = ((IRelationalDbContextOptionsBuilderInfrastructure)builder).OptionsBuilder;
            var ext = new RelationalBatchDbContextOptionsExtension<
                EnhancedQuerySqlGeneratorFactory,
                SqlServerQuerySqlGeneratorFactory,
                SqlServerBatchOperationProvider>("SqlServerBatchExtension");
            ((IDbContextOptionsBuilderInfrastructure)builder1).AddOrUpdateExtension(ext);
            return builder;
        }
    }
}
