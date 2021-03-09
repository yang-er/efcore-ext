using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqliteUpdateJoinTest : UpdateJoinTestBase<SqliteContextFactory<UpdateContext>>
    {
        public SqliteUpdateJoinTest(
            SqliteContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalUpdate()
        {
            base.CompiledQuery_NormalUpdate();

            LogSql(nameof(CompiledQuery_NormalUpdate));

            AssertSql(@"
UPDATE ""ItemA_{{schema}}"" AS ""i""
SET ""Value"" = (""i"".""Value"" + ""t"".""Value"") - @__cc
FROM (
    SELECT ""i0"".""Id"", ""i0"".""Value""
    FROM ""ItemB_{{schema}}"" AS ""i0""
    WHERE ""i0"".""Value"" = @__aa
) AS ""t""
WHERE (""i"".""Id"" = @__bb) AND (""i"".""Id"" = ""t"".""Id"")
");
        }

        public override void LocalTableJoin()
        {
            base.LocalTableJoin();

            LogSql(nameof(LocalTableJoin));

            AssertSql(@"
UPDATE ""ItemB_{{schema}}"" AS ""i""
SET ""Value"" = ""i"".""Value"" + ""cte"".""Value""
FROM (
    VALUES
    (@__p_0_0_0, @__p_0_0_1),
    (@__p_0_1_0, @__p_0_1_1),
    (@__p_0_2_0, @__p_0_2_1)
) AS ""cte"" (""Id"", ""Value"")
WHERE (""i"".""Id"" <> 2) AND (""i"".""Id"" = ""cte"".""Id"")
");
        }

        public override void NormalUpdate()
        {
            base.NormalUpdate();

            LogSql(nameof(NormalUpdate));

            AssertSql(@"
UPDATE ""ItemA_{{schema}}"" AS ""i""
SET ""Value"" = (""i"".""Value"" + ""t"".""Value"") - 3
FROM (
    SELECT ""i0"".""Id"", ""i0"".""Value""
    FROM ""ItemB_{{schema}}"" AS ""i0""
    WHERE ""i0"".""Value"" = 2
) AS ""t""
WHERE (""i"".""Id"" = 1) AND (""i"".""Id"" = ""t"".""Id"")
");
        }
    }
}
