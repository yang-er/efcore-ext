using Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Storage;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUpdateJoinTest : UpdateJoinTestBase<MySqlContextFactory<UpdateContext>>
    {
        public ServerVersion ServerVersion { get; }

        public MySqlUpdateJoinTest(
            MySqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
            ServerVersion = factory.ServerVersion;
        }

        public override void CompiledQuery_NormalUpdate()
        {
            base.CompiledQuery_NormalUpdate();

            AssertSql(@"
UPDATE `ItemA_{{schema}}` AS `i`
INNER JOIN (
    SELECT `i0`.`Id`, `i0`.`Value`
    FROM `ItemB_{{schema}}` AS `i0`
    WHERE `i0`.`Value` = @__aa
) AS `t` ON `i`.`Id` = `t`.`Id`
SET `i`.`Value` = (`i`.`Value` + `t`.`Value`) - @__cc
WHERE `i`.`Id` = @__bb
");
        }

        public override void LocalTableJoin()
        {
            base.LocalTableJoin();

            if (ServerVersion.Type == ServerType.MySql && ServerVersion.Version >= new System.Version(8, 0, 19))
            {
                AssertSql(@"
UPDATE `ItemB_{{schema}}` AS `i`
INNER JOIN (
    VALUES
    ROW(@__p_0_0_0, @__p_0_0_1),
    ROW(@__p_0_1_0, @__p_0_1_1),
    ROW(@__p_0_2_0, @__p_0_2_1)
) AS `cte` (`Id`, `Value`) ON `i`.`Id` = `cte`.`Id`
SET `i`.`Value` = `i`.`Value` + `cte`.`Value`
WHERE `i`.`Id` <> 2
");
            }
            else
            {
                AssertSql(@"
UPDATE `ItemB_{{schema}}` AS `i`
INNER JOIN (
    SELECT @__p_0_0_0 AS `Id`, @__p_0_0_1 AS `Value`
    UNION SELECT @__p_0_1_0, @__p_0_1_1
    UNION SELECT @__p_0_2_0, @__p_0_2_1
) AS `cte` ON `i`.`Id` = `cte`.`Id`
SET `i`.`Value` = `i`.`Value` + `cte`.`Value`
WHERE `i`.`Id` <> 2
");
            }
        }

        public override void NormalUpdate()
        {
            base.NormalUpdate();

            AssertSql(@"
UPDATE `ItemA_{{schema}}` AS `i`
INNER JOIN (
    SELECT `i0`.`Id`, `i0`.`Value`
    FROM `ItemB_{{schema}}` AS `i0`
    WHERE `i0`.`Value` = 2
) AS `t` ON `i`.`Id` = `t`.`Id`
SET `i`.`Value` = (`i`.`Value` + `t`.`Value`) - 3
WHERE `i`.`Id` = 1
");
        }
    }
}
