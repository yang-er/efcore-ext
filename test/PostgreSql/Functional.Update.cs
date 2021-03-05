using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUpdateTest : UpdateTestBase<NpgsqlContextFactory<UpdateContext>>
    {
        public NpgsqlUpdateTest(
            NpgsqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConstantUpdateBody()
        {
            base.CompiledQuery_ConstantUpdateBody();

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Description"" = 'Updated', ""Price"" = 1.5
WHERE (i.""ItemId"" <= 388) AND (i.""Price"" >= @__price)
");
        }

        public override void CompiledQuery_NavigationSelect()
        {
            base.CompiledQuery_NavigationSelect();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = (d.""Another"" + j.""SubmissionId"") + @__x
FROM ""Judging_{{schema}}"" AS j
WHERE d.""JudgingId"" = j.""JudgingId""
");
        }

        public override void CompiledQuery_NavigationWhere()
        {
            base.CompiledQuery_NavigationWhere();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = j.""SubmissionId""
FROM ""Judging_{{schema}}"" AS j
WHERE (j.""PreviousJudgingId"" = @__x) AND (d.""JudgingId"" = j.""JudgingId"")
");
        }

        public override void CompiledQuery_ParameterUpdateBody()
        {
            base.CompiledQuery_ParameterUpdateBody();

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Description"" = @__desc, ""Price"" = @__pri
WHERE (i.""ItemId"" <= 388) AND (i.""Price"" >= @__price)
");
        }

        public override void CompiledQuery_ScalarSubquery()
        {
            base.CompiledQuery_ScalarSubquery();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = (
    SELECT COUNT(*)::INT
    FROM ""Item_{{schema}}"" AS i)
");
        }

        public override void ConstantUpdateBody()
        {
            base.ConstantUpdateBody();

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Description"" = 'Updated', ""Price"" = 1.5
WHERE (i.""ItemId"" <= 388) AND (i.""Price"" >= @__price_0)
");
        }

        public override void NavigationSelect()
        {
            base.NavigationSelect();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = (d.""Another"" + j.""SubmissionId"") + @__x_0
FROM ""Judging_{{schema}}"" AS j
WHERE d.""JudgingId"" = j.""JudgingId""
");
        }

        public override void NavigationWhere()
        {
            base.NavigationWhere();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = j.""SubmissionId""
FROM ""Judging_{{schema}}"" AS j
WHERE (j.""PreviousJudgingId"" = @__x_0) AND (d.""JudgingId"" = j.""JudgingId"")
");
        }

        public override void ParameterUpdateBody()
        {
            base.ParameterUpdateBody();

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Description"" = @__desc_1, ""Price"" = @__pri_2
WHERE (i.""ItemId"" <= 388) AND (i.""Price"" >= @__price_0)
");
        }

        public override void ScalarSubquery()
        {
            base.ScalarSubquery();

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS d
SET ""Another"" = (
    SELECT COUNT(*)::INT
    FROM ""Item_{{schema}}"" AS i)
");
        }

        public override void SetNull()
        {
            base.SetNull();

            AssertSql31(@"
UPDATE ""Judging_{{schema}}"" AS j
SET ""CompileError"" = NULL, ""ExecuteMemory"" = NULL, ""PreviousJudgingId"" = NULL, ""TotalScore"" = NULL, ""StartTime"" = NOW(), ""Server"" = NULL, ""Status"" = GREATEST(j.""Status"", 8)
");

            AssertSql50(@"
UPDATE ""Judging_{{schema}}"" AS j
SET ""CompileError"" = NULL, ""ExecuteMemory"" = NULL, ""PreviousJudgingId"" = NULL, ""TotalScore"" = NULL, ""StartTime"" = NOW(), ""Server"" = NULL, ""Status"" = greatest(j.""Status"", 8)
");
        }

        public override void ConcatenateBody()
        {
            base.ConcatenateBody();

            AssertSql31(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Name"" = i.""Name"" || @__suffix_1, ""Quantity"" = i.""Quantity"" + @__incrementStep_2
WHERE (i.""ItemId"" <= 500) AND (i.""Price"" >= @__price_0)
");

            AssertSql50(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Name"" = COALESCE(i.""Name"", '') || @__suffix_1, ""Quantity"" = i.""Quantity"" + @__incrementStep_2
WHERE (i.""ItemId"" <= 500) AND (i.""Price"" >= @__price_0)
");
        }

        public override void CompiledQuery_ConcatenateBody()
        {
            base.CompiledQuery_ConcatenateBody();

            AssertSql31(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Name"" = i.""Name"" || @__suffix, ""Quantity"" = i.""Quantity"" + @__incrementStep
WHERE (i.""ItemId"" <= 500) AND (i.""Price"" >= @__price)
");

            AssertSql50(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Name"" = COALESCE(i.""Name"", '') || @__suffix, ""Quantity"" = i.""Quantity"" + @__incrementStep
WHERE (i.""ItemId"" <= 500) AND (i.""Price"" >= @__price)
");
        }

        public override void CompiledQuery_SetNull()
        {
            base.CompiledQuery_SetNull();

            AssertSql31(@"
UPDATE ""Judging_{{schema}}"" AS j
SET ""CompileError"" = NULL, ""ExecuteMemory"" = NULL, ""PreviousJudgingId"" = NULL, ""TotalScore"" = NULL, ""StartTime"" = NOW(), ""Server"" = NULL, ""Status"" = GREATEST(j.""Status"", 8)
");

            AssertSql50(@"
UPDATE ""Judging_{{schema}}"" AS j
SET ""CompileError"" = NULL, ""ExecuteMemory"" = NULL, ""PreviousJudgingId"" = NULL, ""TotalScore"" = NULL, ""StartTime"" = NOW(), ""Server"" = NULL, ""Status"" = greatest(j.""Status"", 8)
");
        }

        public override void CompiledQuery_HasOwnedType()
        {
            base.CompiledQuery_HasOwnedType();

            AssertSql(@"
UPDATE ""ChangeLog_{{schema}}"" AS c
SET ""Audit_IsDeleted"" = NOT (c.""Audit_IsDeleted"")
");
        }

        public override void HasOwnedType()
        {
            base.HasOwnedType();

            AssertSql(@"
UPDATE ""ChangeLog_{{schema}}"" AS c
SET ""Audit_IsDeleted"" = NOT (c.""Audit_IsDeleted"")
");
        }
    }
}
