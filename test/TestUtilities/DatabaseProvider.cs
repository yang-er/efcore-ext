using System;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [Flags]
    public enum DatabaseProvider
    {
        None = 0,

        InMemory_31 = 1 << 0,
        InMemory_50 = 1 << 1,

        SqlServer_31 = 1 << 2,
        SqlServer_50 = 1 << 3,

        PostgreSQL_31 = 1 << 4,
        PostgreSQL_50 = 1 << 5,

        InMemory = InMemory_31 | InMemory_50,
        SqlServer = SqlServer_31 | SqlServer_50,
        PostgreSQL = PostgreSQL_31 | PostgreSQL_50,

        Relational31 = SqlServer_31 | PostgreSQL_31,
        Relational50 = SqlServer_50 | PostgreSQL_50,
        Relational = SqlServer | PostgreSQL,

        Version_31 = InMemory_31 | Relational31,
        Version_50 = InMemory_50 | Relational50,
        All = InMemory | Relational
    }
}
