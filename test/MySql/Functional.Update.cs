using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUpdateTest : UpdateTestBase<MySqlContextFactory<UpdateContext>>
    {
        public MySqlUpdateTest(
            MySqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConcatenateBody()
        {
            base.CompiledQuery_ConcatenateBody();

            LogSql(nameof(CompiledQuery_ConcatenateBody));

            AssertSql(V31, @"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Name` = CONCAT(`i`.`Name`, @__suffix), `i`.`Quantity` = `i`.`Quantity` + @__incrementStep
WHERE `i`.`ItemId` <= 500
");

            AssertSql(V50 | V60, @"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Name` = CONCAT(COALESCE(`i`.`Name`, ''), @__suffix), `i`.`Quantity` = `i`.`Quantity` + @__incrementStep
WHERE `i`.`ItemId` <= 500
");
        }

        public override void CompiledQuery_ConstantUpdateBody()
        {
            base.CompiledQuery_ConstantUpdateBody();

            LogSql(nameof(CompiledQuery_ConstantUpdateBody));

            AssertSql(@"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Description` = 'Updated', `i`.`Price` = 1.5
WHERE `i`.`ItemId` <= 388
");
        }

        public override void CompiledQuery_HasOwnedType()
        {
            base.CompiledQuery_HasOwnedType();

            LogSql(nameof(CompiledQuery_HasOwnedType));

            AssertSql(V31, @"
UPDATE `ChangeLog_{{schema}}` AS `c`
LEFT JOIN (
    SELECT `c0`.`ChangeLogId`, `c0`.`ChangedBy`, `c0`.`Audit_IsDeleted`
    FROM `ChangeLog_{{schema}}` AS `c0`
    WHERE `c0`.`Audit_IsDeleted` IS NOT NULL
) AS `t` ON `c`.`ChangeLogId` = `t`.`ChangeLogId`
SET `c`.`Audit_IsDeleted` = NOT (`t`.`Audit_IsDeleted`)
");

            AssertSql(V50 | V60, @"
UPDATE `ChangeLog_{{schema}}` AS `c`
SET `c`.`Audit_IsDeleted` = NOT (`c`.`Audit_IsDeleted`)
");
        }

        public override void CompiledQuery_NavigationSelect()
        {
            base.CompiledQuery_NavigationSelect();

            LogSql(nameof(CompiledQuery_NavigationSelect));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
INNER JOIN `Judging_{{schema}}` AS `j` ON `d`.`JudgingId` = `j`.`JudgingId`
SET `d`.`Another` = (`d`.`Another` + `j`.`SubmissionId`) + @__x
");
        }

        public override void CompiledQuery_NavigationWhere()
        {
            base.CompiledQuery_NavigationWhere();

            LogSql(nameof(CompiledQuery_NavigationWhere));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
INNER JOIN `Judging_{{schema}}` AS `j` ON `d`.`JudgingId` = `j`.`JudgingId`
SET `d`.`Another` = `j`.`SubmissionId`
WHERE `j`.`PreviousJudgingId` = @__x
");
        }

        public override void CompiledQuery_ParameterUpdateBody()
        {
            base.CompiledQuery_ParameterUpdateBody();

            LogSql(nameof(CompiledQuery_ParameterUpdateBody));

            AssertSql(@"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Description` = @__desc, `i`.`Price` = @__pri
WHERE `i`.`ItemId` <= 388
");
        }

        public override void CompiledQuery_ScalarSubquery()
        {
            base.CompiledQuery_ScalarSubquery();

            LogSql(nameof(CompiledQuery_ScalarSubquery));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
SET `d`.`Another` = (
    SELECT COUNT(*)
    FROM `Item_{{schema}}` AS `i`)
");
        }

        public override void CompiledQuery_SetNull()
        {
            base.CompiledQuery_SetNull();

            LogSql(nameof(CompiledQuery_SetNull));

            AssertSql(@"
UPDATE `Judging_{{schema}}` AS `j`
SET `j`.`CompileError` = NULL, `j`.`ExecuteMemory` = NULL, `j`.`PreviousJudgingId` = NULL, `j`.`TotalScore` = NULL, `j`.`Server` = NULL, `j`.`Status` = GREATEST(`j`.`Status`, 8)
");
        }

        public override void ConcatenateBody()
        {
            base.ConcatenateBody();

            LogSql(nameof(ConcatenateBody));

            AssertSql(V31, @"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Name` = CONCAT(`i`.`Name`, @__suffix_1), `i`.`Quantity` = `i`.`Quantity` + @__incrementStep_2
WHERE (`i`.`ItemId` <= 500) AND (`i`.`Price` >= @__price_0)
");

            AssertSql(V50 | V60, @"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Name` = CONCAT(COALESCE(`i`.`Name`, ''), @__suffix_1), `i`.`Quantity` = `i`.`Quantity` + @__incrementStep_2
WHERE (`i`.`ItemId` <= 500) AND (`i`.`Price` >= @__price_0)
");
        }

        public override void ConstantUpdateBody()
        {
            base.ConstantUpdateBody();

            LogSql(nameof(ConstantUpdateBody));

            AssertSql(@"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Description` = 'Updated', `i`.`Price` = 1.5
WHERE (`i`.`ItemId` <= 388) AND (`i`.`Price` >= @__price_0)
");
        }

        public override void HasOwnedType()
        {
            base.HasOwnedType();

            LogSql(nameof(HasOwnedType));

            AssertSql(V31, @"
UPDATE `ChangeLog_{{schema}}` AS `c`
LEFT JOIN (
    SELECT `c0`.`ChangeLogId`, `c0`.`ChangedBy`, `c0`.`Audit_IsDeleted`
    FROM `ChangeLog_{{schema}}` AS `c0`
    WHERE `c0`.`Audit_IsDeleted` IS NOT NULL
) AS `t` ON `c`.`ChangeLogId` = `t`.`ChangeLogId`
SET `c`.`Audit_IsDeleted` = NOT (`t`.`Audit_IsDeleted`)
");

            AssertSql(V50 | V60, @"
UPDATE `ChangeLog_{{schema}}` AS `c`
SET `c`.`Audit_IsDeleted` = NOT (`c`.`Audit_IsDeleted`)
");
        }

        public override void NavigationSelect()
        {
            base.NavigationSelect();

            LogSql(nameof(NavigationSelect));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
INNER JOIN `Judging_{{schema}}` AS `j` ON `d`.`JudgingId` = `j`.`JudgingId`
SET `d`.`Another` = (`d`.`Another` + `j`.`SubmissionId`) + @__x_0
");
        }

        public override void NavigationWhere()
        {
            base.NavigationWhere();

            LogSql(nameof(NavigationWhere));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
INNER JOIN `Judging_{{schema}}` AS `j` ON `d`.`JudgingId` = `j`.`JudgingId`
SET `d`.`Another` = `j`.`SubmissionId`
WHERE `j`.`PreviousJudgingId` = @__x_0
");
        }

        public override void ParameterUpdateBody()
        {
            base.ParameterUpdateBody();

            LogSql(nameof(ParameterUpdateBody));

            AssertSql(@"
UPDATE `Item_{{schema}}` AS `i`
SET `i`.`Description` = @__desc_1, `i`.`Price` = @__pri_2
WHERE (`i`.`ItemId` <= 388) AND (`i`.`Price` >= @__price_0)
");
        }

        public override void ScalarSubquery()
        {
            base.ScalarSubquery();

            LogSql(nameof(ScalarSubquery));

            AssertSql(@"
UPDATE `Detail_{{schema}}` AS `d`
SET `d`.`Another` = (
    SELECT COUNT(*)
    FROM `Item_{{schema}}` AS `i`)
");
        }

        public override void SetNull()
        {
            base.SetNull();

            LogSql(nameof(SetNull));

            AssertSql(@"
UPDATE `Judging_{{schema}}` AS `j`
SET `j`.`CompileError` = NULL, `j`.`ExecuteMemory` = NULL, `j`.`PreviousJudgingId` = NULL, `j`.`TotalScore` = NULL, `j`.`StartTime` = UTC_TIMESTAMP(), `j`.`Server` = NULL, `j`.`Status` = GREATEST(`j`.`Status`, 8)
");
        }
    }
}
