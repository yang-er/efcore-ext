using System;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [Flags]
    public enum DatabaseProvider
    {
        None = 0,
        InMemory = 1 << 0,
        SqlServer = 1 << 1,
        PostgreSQL = 1 << 2,
        Relational = SqlServer | PostgreSQL,
    }
}
