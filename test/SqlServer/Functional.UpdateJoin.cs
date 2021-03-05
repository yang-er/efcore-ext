using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUpdateJoinTest : UpdateJoinTestBase<SqlServerContextFactory<UpdateContext>>
    {
        public SqlServerUpdateJoinTest(
            SqlServerContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalUpdate()
        {
            base.CompiledQuery_NormalUpdate();

            LogSql(nameof(CompiledQuery_NormalUpdate));

            AssertSql(@"
UPDATE [i]
SET [i].[Value] = ([i].[Value] + [t].[Value]) - @__cc
FROM [ItemA_{{schema}}] AS [i]
INNER JOIN (
    SELECT [i0].[Id], [i0].[Value]
    FROM [ItemB_{{schema}}] AS [i0]
    WHERE [i0].[Value] = @__aa
) AS [t] ON [i].[Id] = [t].[Id]
WHERE [i].[Id] = @__bb
");
        }

        public override void LocalTableJoin()
        {
            base.LocalTableJoin();

            LogSql(nameof(LocalTableJoin));

            AssertSql(@"
UPDATE [i]
SET [i].[Value] = [i].[Value] + [cte].[Value]
FROM [ItemB_{{schema}}] AS [i]
INNER JOIN (
    VALUES
    (@__p_0_0_0, @__p_0_0_1),
    (@__p_0_1_0, @__p_0_1_1),
    (@__p_0_2_0, @__p_0_2_1)
) AS [cte] ([Id], [Value]) ON [i].[Id] = [cte].[Id]
WHERE [i].[Id] <> 2
");
        }

        public override void NormalUpdate()
        {
            base.NormalUpdate();

            LogSql(nameof(NormalUpdate));

            AssertSql(@"
UPDATE [i]
SET [i].[Value] = ([i].[Value] + [t].[Value]) - 3
FROM [ItemA_{{schema}}] AS [i]
INNER JOIN (
    SELECT [i0].[Id], [i0].[Value]
    FROM [ItemB_{{schema}}] AS [i0]
    WHERE [i0].[Value] = 2
) AS [t] ON [i].[Id] = [t].[Id]
WHERE [i].[Id] = 1
");
        }
    }
}
