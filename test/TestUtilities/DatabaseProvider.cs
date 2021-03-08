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

        Sqlite_31 = 1 << 6,
        Sqlite_50 = 1 << 7,

        InMemory = InMemory_31 | InMemory_50,
        SqlServer = SqlServer_31 | SqlServer_50,
        PostgreSQL = PostgreSQL_31 | PostgreSQL_50,
        Sqlite = Sqlite_31 | Sqlite_50,

        Relational_31 = SqlServer_31 | PostgreSQL_31 | Sqlite_31,
        Relational_50 = SqlServer_50 | PostgreSQL_50 | Sqlite_50,
        Relational = SqlServer | PostgreSQL | Sqlite,

        Version_31 = InMemory_31 | Relational_31,
        Version_50 = InMemory_50 | Relational_50,
        All = InMemory | Relational
    }
}
