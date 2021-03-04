using Microsoft.EntityFrameworkCore.Tests.Upsert;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUpsertTest : UpsertTestBase<NpgsqlContextFactory<UpsertContext>>
    {
        public NpgsqlUpsertTest(
            NpgsqlContextFactory<UpsertContext> factory)
            : base(factory)
        {
        }
    }
}
