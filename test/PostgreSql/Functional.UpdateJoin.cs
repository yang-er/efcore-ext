using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUpdateJoinTest : UpdateJoinTestBase<NpgsqlContextFactory<UpdateContext>>
    {
        public NpgsqlUpdateJoinTest(
            NpgsqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }
    }
}
