using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public class DatabaseCollection :
        DatabaseCollection<InMemoryContextFactory<PrepareContext>>,
        ICollectionFixture<DatabaseCollection>
    {
        public DatabaseCollection() : base(new InMemoryContextFactory<PrepareContext>())
        {
        }
    }
}
