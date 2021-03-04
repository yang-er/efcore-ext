using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public class DatabaseCollection :
        DatabaseCollection<SqlServerContextFactory<PrepareContext>>,
        ICollectionFixture<DatabaseCollection>
    {
        public DatabaseCollection() : base(new SqlServerContextFactory<PrepareContext>())
        {
        }
    }
}
