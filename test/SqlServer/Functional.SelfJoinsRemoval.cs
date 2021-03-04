using Microsoft.EntityFrameworkCore.Tests.SelfJoinsRemoval;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUselessJoinsRemovalTest : UselessJoinsRemovalTestBase<SqlServerContextFactory<TestingContext>>
    {
        public SqlServerUselessJoinsRemovalTest(
            SqlServerContextFactory<TestingContext> factory)
            : base(factory)
        {
        }
    }
}
