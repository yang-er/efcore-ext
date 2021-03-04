using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryDeleteTest : DeleteTestBase<InMemoryContextFactory<DeleteContext>>
    {
        public InMemoryDeleteTest(
            InMemoryContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }
    }
}
