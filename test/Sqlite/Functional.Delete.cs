using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqliteDeleteTest : DeleteTestBase<SqliteContextFactory<DeleteContext>>
    {
        public SqliteDeleteTest(
            SqliteContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConstantCondition()
        {
            base.CompiledQuery_ConstantCondition();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE (""i"".""ItemId"" > 500) AND (""i"".""Price"" = '3.0')
");
        }

        public override void CompiledQuery_ContainsSomething()
        {
            base.CompiledQuery_ContainsSomething();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Name"" IN ('jyntnytjyntjntnytnt', 'aaa')
");
        }

        public override void CompiledQuery_ParameteredCondition()
        {
            base.CompiledQuery_ParameteredCondition();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Name"" = @__nameToDelete
");
        }

        public override void ConstantCondition()
        {
            base.ConstantCondition();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE (""i"".""ItemId"" > 500) AND (""i"".""Price"" = '124.0')
");
        }

        public override void ContainsAndAlsoEqual()
        {
            base.ContainsAndAlsoEqual();

            AssertSql(V31, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Description"" IN ('info') OR (""i"".""Name"" = @__nameToDelete_1)
");

            AssertSql(V50 | V60, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE (""i"".""Description"" = 'info') OR (""i"".""Name"" = @__nameToDelete_1)
");
        }

        public override void ContainsSomething()
        {
            base.ContainsSomething();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Description"" IN ('info', 'aaa')
");
        }

        public override void EmptyContains()
        {
            base.EmptyContains();

            AssertSql(V31, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE 1 = 0
");

            AssertSql(V50 | V60, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE 0
");
        }

        public override void ListAny()
        {
            base.ListAny();

            AssertSql(V31, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Description"" IN ('info')
");

            AssertSql(V50 | V60, @"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Description"" = 'info'
");
        }

        public override void ParameteredCondition()
        {
            base.ParameteredCondition();

            AssertSql(@"
DELETE FROM ""Item_{{schema}}"" AS ""i""
WHERE ""i"".""Name"" = @__nameToDelete_0
");
        }
    }
}
