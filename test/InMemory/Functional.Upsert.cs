using Microsoft.EntityFrameworkCore.Tests.Upsert;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryUpsertTest : UpsertTestBase<InMemoryContextFactory<UpsertContext>>
    {
        public InMemoryUpsertTest(
            InMemoryContextFactory<UpsertContext> factory)
            : base(factory)
        {
        }
    }
}
