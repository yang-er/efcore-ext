using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlDeleteTest : DeleteTestBase<NpgsqlContextFactory<DeleteContext>>
    {
        public NpgsqlDeleteTest(
            NpgsqlContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }
    }
}
