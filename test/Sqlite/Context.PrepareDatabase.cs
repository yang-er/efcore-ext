using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public class DatabaseCollection :
        DatabaseCollection<SqliteContextFactory<PrepareContext>>,
        ICollectionFixture<DatabaseCollection>
    {
        public DatabaseCollection() : base(new SqliteContextFactory<PrepareContext>())
        {
        }
    }
}
