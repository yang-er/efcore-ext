using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore
{
    public static class TableSplittingJoinsRemovalExtensions
    {
        public static DbContextOptionsBuilder UseTableSplittingJoinsRemoval(this DbContextOptionsBuilder builder)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new TableSplittingJoinsRemovalDbContextOptionsExtension());
            return builder;
        }
    }
}
