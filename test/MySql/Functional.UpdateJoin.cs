using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUpdateJoinTest : UpdateJoinTestBase<MySqlContextFactory<UpdateContext>>
    {
        public MySqlUpdateJoinTest(
            MySqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalUpdate()
        {
            base.CompiledQuery_NormalUpdate();

            LogSql(nameof(CompiledQuery_NormalUpdate));

            AssertSql(@"

");
        }

        public override void LocalTableJoin()
        {
            base.LocalTableJoin();

            LogSql(nameof(LocalTableJoin));

            AssertSql(@"

");
        }

        public override void NormalUpdate()
        {
            base.NormalUpdate();

            LogSql(nameof(NormalUpdate));

            AssertSql(@"

");
        }
    }
}
