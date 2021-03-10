using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public class DatabaseCollection :
        DatabaseCollection<MySqlContextFactory<PrepareContext>>,
        ICollectionFixture<DatabaseCollection>
    {
        public DatabaseCollection() : base(new MySqlContextFactory<PrepareContext>())
        {
        }
    }
}
