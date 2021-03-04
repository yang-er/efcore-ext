using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUpdateJoinTest : UpdateJoinTestBase<SqlServerContextFactory<UpdateContext>>
    {
        public SqlServerUpdateJoinTest(
            SqlServerContextFactory<UpdateContext> factory,
            DataFixture<SqlServerContextFactory<UpdateContext>> dataFixture)
            : base(factory, dataFixture)
        {
        }
    }
}
