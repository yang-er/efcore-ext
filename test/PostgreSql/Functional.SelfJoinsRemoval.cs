using Microsoft.EntityFrameworkCore.Tests.SelfJoinsRemoval;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUselessJoinsRemovalTest : UselessJoinsRemovalTestBase<NpgsqlContextFactory<TestingContext>>
    {
        public NpgsqlUselessJoinsRemovalTest(
            NpgsqlContextFactory<TestingContext> factory)
            : base(factory)
        {
        }
    }
}
