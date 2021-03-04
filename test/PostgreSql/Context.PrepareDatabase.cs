using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public class DatabaseCollection :
        DatabaseCollection<NpgsqlContextFactory<PrepareContext>>,
        ICollectionFixture<DatabaseCollection>
    {
        public DatabaseCollection() : base(new NpgsqlContextFactory<PrepareContext>())
        {
        }
    }
}
