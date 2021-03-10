using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlDeleteTest : DeleteTestBase<MySqlContextFactory<DeleteContext>>
    {
        public MySqlDeleteTest(
            MySqlContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConstantCondition()
        {
            base.CompiledQuery_ConstantCondition();

            LogSql(nameof(CompiledQuery_ConstantCondition));

            AssertSql(@"

");
        }

        public override void CompiledQuery_ContainsSomething()
        {
            base.CompiledQuery_ContainsSomething();

            LogSql(nameof(CompiledQuery_ContainsSomething));

            AssertSql(@"

");
        }

        public override void CompiledQuery_ParameteredCondition()
        {
            base.CompiledQuery_ParameteredCondition();

            LogSql(nameof(CompiledQuery_ParameteredCondition));

            AssertSql(@"

");
        }

        public override void ConstantCondition()
        {
            base.ConstantCondition();

            LogSql(nameof(ConstantCondition));

            AssertSql(@"

");
        }

        public override void ContainsAndAlsoEqual()
        {
            base.ContainsAndAlsoEqual();

            LogSql(nameof(ContainsAndAlsoEqual));

            AssertSql(@"

");
        }

        public override void ContainsSomething()
        {
            base.ContainsSomething();

            LogSql(nameof(ContainsSomething));

            AssertSql(@"

");
        }

        public override void EmptyContains()
        {
            base.EmptyContains();

            LogSql(nameof(EmptyContains));

            AssertSql(@"

");
        }

        public override void ListAny()
        {
            base.ListAny();

            LogSql(nameof(ListAny));

            AssertSql(@"

");
        }

        public override void ParameteredCondition()
        {
            base.ParameteredCondition();

            LogSql(nameof(ParameteredCondition));

            AssertSql(@"

");
        }
    }
}
