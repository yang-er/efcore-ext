using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerDeleteTest : DeleteTestBase<SqlServerContextFactory<DeleteContext>>
    {
        public SqlServerDeleteTest(
            SqlServerContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConstantCondition()
        {
            base.CompiledQuery_ConstantCondition();

            LogSql(nameof(CompiledQuery_ConstantCondition));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] > 500) AND ([i].[Price] = 3.0)
");
        }

        public override void CompiledQuery_ContainsSomething()
        {
            base.CompiledQuery_ContainsSomething();

            LogSql(nameof(CompiledQuery_ContainsSomething));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Name] IN (N'jyntnytjyntjntnytnt', N'aaa')
");
        }

        public override void CompiledQuery_ParameteredCondition()
        {
            base.CompiledQuery_ParameteredCondition();

            LogSql(nameof(CompiledQuery_ParameteredCondition));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Name] = @__nameToDelete
");
        }

        public override void ConstantCondition()
        {
            base.ConstantCondition();

            LogSql(nameof(ConstantCondition));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] > 500) AND ([i].[Price] = 124.0)
");
        }

        public override void ContainsAndAlsoEqual()
        {
            base.ContainsAndAlsoEqual();

            LogSql(nameof(ContainsAndAlsoEqual));

            AssertSql31(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Description] IN (N'info') OR ([i].[Name] = @__nameToDelete_1)
");

            AssertSql50(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[Description] = N'info') OR ([i].[Name] = @__nameToDelete_1)
");
        }

        public override void ContainsSomething()
        {
            base.ContainsSomething();

            LogSql(nameof(ContainsSomething));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Description] IN (N'info', N'aaa')
");
        }

        public override void EmptyContains()
        {
            base.EmptyContains();

            LogSql(nameof(EmptyContains));

            AssertSql31(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE CAST(1 AS bit) = CAST(0 AS bit)
");

            AssertSql50(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE 0 = 1
");
        }

        public override void ListAny()
        {
            base.ListAny();

            LogSql(nameof(ListAny));

            AssertSql31(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Description] IN (N'info')
");

            AssertSql50(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Description] = N'info'
");
        }

        public override void ParameteredCondition()
        {
            base.ParameteredCondition();

            LogSql(nameof(ParameteredCondition));

            AssertSql(@"
DELETE [i]
FROM [Item_{{schema}}] AS [i]
WHERE [i].[Name] = @__nameToDelete_0
");
        }
    }
}
