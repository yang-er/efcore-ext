using Microsoft.EntityFrameworkCore.Tests.SelfJoinsRemoval;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUselessJoinsRemovalTest : UselessJoinsRemovalTestBase<MySqlContextFactory<TestingContext>>
    {
        public MySqlUselessJoinsRemovalTest(
            MySqlContextFactory<TestingContext> factory)
            : base(factory)
        {
        }

        public override void GroupJoin_2_3()
        {
            base.GroupJoin_2_3();

            LogSql(nameof(GroupJoin_2_3));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void GroupJoin_3_2()
        {
            base.GroupJoin_3_2();

            LogSql(nameof(GroupJoin_3_2));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void HasOneWithOne_SharedTable()
        {
            base.HasOneWithOne_SharedTable();

            LogSql(nameof(HasOneWithOne_SharedTable));

            AssertSql(@"

");
        }

        public override void InnerJoin_2_3()
        {
            base.InnerJoin_2_3();

            LogSql(nameof(InnerJoin_2_3));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void InnerJoin_3_2()
        {
            base.InnerJoin_3_2();

            LogSql(nameof(InnerJoin_3_2));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void Owned()
        {
            base.Owned();

            LogSql(nameof(Owned));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void OwnedThenUnionDistinct()
        {
            base.OwnedThenUnionDistinct();

            LogSql(nameof(OwnedThenUnionDistinct));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void Owned_SkipTrimming()
        {
            base.Owned_SkipTrimming();

            LogSql(nameof(Owned_SkipTrimming));

            AssertSql(@"

");
        }

        public override void ReallyJoin()
        {
            base.ReallyJoin();

            LogSql(nameof(ReallyJoin));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void ReallyUnionDistinct()
        {
            base.ReallyUnionDistinct();

            LogSql(nameof(ReallyUnionDistinct));

            AssertSql(@"

");
        }

        public override void ShaperChanged()
        {
            base.ShaperChanged();

            LogSql(nameof(ShaperChanged));

            AssertSql(@"

");
        }

        public override void SuperOwned()
        {
            base.SuperOwned();

            LogSql(nameof(SuperOwned));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }
    }
}
