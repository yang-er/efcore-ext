using System;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [Flags]
    public enum DatabaseProvider
    {
        None = 0,

        InMemory_31 = 1 << 0,
        InMemory_50 = 1 << 1,
        InMemory_60 = 1 << 2,

        SqlServer_31 = 1 << 3,
        SqlServer_50 = 1 << 4,
        SqlServer_60 = 1 << 5,

        PostgreSQL_31 = 1 << 6,
        PostgreSQL_50 = 1 << 7,
        PostgreSQL_60 = 1 << 8,

        Sqlite_31 = 1 << 9,
        Sqlite_50 = 1 << 10,
        Sqlite_60 = 1 << 11,

        MySql_31 = 1 << 12,
        MySql_50 = 1 << 13,
        MySql_60 = 1 << 14,

        InMemory = InMemory_31 | InMemory_50 | InMemory_60,
        SqlServer = SqlServer_31 | SqlServer_50 | SqlServer_60,
        PostgreSQL = PostgreSQL_31 | PostgreSQL_50 | PostgreSQL_60,
        Sqlite = Sqlite_31 | Sqlite_50 | Sqlite_60,
        MySql = MySql_31 | MySql_50 | MySql_60,

        Relational_31 = SqlServer_31 | PostgreSQL_31 | Sqlite_31 | MySql_31,
        Relational_50 = SqlServer_50 | PostgreSQL_50 | Sqlite_50 | MySql_50,
        Relational_60 = SqlServer_60 | PostgreSQL_60 | Sqlite_60 | MySql_60,
        Relational = SqlServer | PostgreSQL | Sqlite | MySql,

        Version_31 = InMemory_31 | Relational_31,
        Version_50 = InMemory_50 | Relational_50,
        Version_60 = InMemory_60 | Relational_60,
        All = InMemory | Relational
    }
}
