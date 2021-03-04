using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryUpdateJoinTest : UpdateJoinTestBase<InMemoryContextFactory<UpdateContext>>
    {
        public InMemoryUpdateJoinTest(
            InMemoryContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }
    }
}
