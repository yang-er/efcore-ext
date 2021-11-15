using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlInsertIntoTest : InsertIntoTestBase<MySqlContextFactory<SelectIntoContext>>
    {
        public MySqlInsertIntoTest(
            MySqlContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalSelectInto()
        {
            base.CompiledQuery_NormalSelectInto();

            LogSql(nameof(CompiledQuery_NormalSelectInto));

            AssertSql(@"
INSERT INTO `ChangeLog_{{schema}}` (`Description`, `ChangedBy`, `Audit_IsDeleted`)
SELECT CONCAT(COALESCE(`j`.`Server`, @__hh), '666') AS `Description`, `j`.`CompileError` AS `ChangedBy`, TRUE AS `Audit_IsDeleted`
FROM `Judging_{{schema}}` AS `j`
");
        }

        public override void CompiledQuery_WithAbstractType()
        {
            base.CompiledQuery_WithAbstractType();

            LogSql(nameof(CompiledQuery_WithAbstractType));

            AssertSql(@"
INSERT INTO `Person_{{schema}}` (`Name`, `Class`)
SELECT `p`.`Name`, `p`.`Subject` AS `Class`
FROM `Person_{{schema}}` AS `p`
WHERE `p`.`Discriminator` = 'Student'
");
        }

        public override void CompiledQuery_WithComputedColumn()
        {
            base.CompiledQuery_WithComputedColumn();

            LogSql(nameof(CompiledQuery_WithComputedColumn));

            AssertSql(V31, @"
INSERT INTO `Document_{{schema}}` (`Content`)
SELECT CONCAT(`d`.`Content`, CONVERT(`d`.`ContentLength`, CHAR(11))) AS `Content`
FROM `Document_{{schema}}` AS `d`
");

            AssertSql(V50 | V60, @"
INSERT INTO `Document_{{schema}}` (`Content`)
SELECT CONCAT(`d`.`Content`, CAST(`d`.`ContentLength` AS char) COLLATE utf8mb4_bin) AS `Content`
FROM `Document_{{schema}}` AS `d`
");
        }

        public override void NormalSelectInto()
        {
            base.NormalSelectInto();

            LogSql(nameof(NormalSelectInto));

            AssertSql(@"
INSERT INTO `ChangeLog_{{schema}}` (`Description`, `ChangedBy`, `Audit_IsDeleted`)
SELECT CONCAT(COALESCE(`j`.`Server`, @__hh_0), '666') AS `Description`, `j`.`CompileError` AS `ChangedBy`, TRUE AS `Audit_IsDeleted`
FROM `Judging_{{schema}}` AS `j`
");
        }

        public override void WithAbstractType()
        {
            base.WithAbstractType();

            LogSql(nameof(WithAbstractType));

            AssertSql(@"
INSERT INTO `Person_{{schema}}` (`Name`, `Class`)
SELECT `p`.`Name`, `p`.`Subject` AS `Class`
FROM `Person_{{schema}}` AS `p`
WHERE `p`.`Discriminator` = 'Student'
");
        }

        public override void WithComputedColumn()
        {
            base.WithComputedColumn();

            LogSql(nameof(WithComputedColumn));

            AssertSql(V31, @"
INSERT INTO `Document_{{schema}}` (`Content`)
SELECT CONCAT(`d`.`Content`, CONVERT(`d`.`ContentLength`, CHAR(11))) AS `Content`
FROM `Document_{{schema}}` AS `d`
");

            AssertSql(V50 | V60, @"
INSERT INTO `Document_{{schema}}` (`Content`)
SELECT CONCAT(`d`.`Content`, CAST(`d`.`ContentLength` AS char) COLLATE utf8mb4_bin) AS `Content`
FROM `Document_{{schema}}` AS `d`
");
        }
    }
}
