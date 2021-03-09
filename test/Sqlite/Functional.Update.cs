using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;
using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqliteUpdateTest : UpdateTestBase<SqliteContextFactory<UpdateContext>>
    {
        public SqliteUpdateTest(
            SqliteContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConcatenateBody()
        {
            base.CompiledQuery_ConcatenateBody();

            LogSql(nameof(CompiledQuery_ConcatenateBody));

            AssertSql31(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Name"" = ""i"".""Name"" || @__suffix, ""Quantity"" = ""i"".""Quantity"" + @__incrementStep
WHERE ""i"".""ItemId"" <= 500
");

            AssertSql50(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Name"" = COALESCE(""i"".""Name"", '') || @__suffix, ""Quantity"" = ""i"".""Quantity"" + @__incrementStep
WHERE ""i"".""ItemId"" <= 500
");
        }

        public override void CompiledQuery_ConstantUpdateBody()
        {
            base.CompiledQuery_ConstantUpdateBody();

            LogSql(nameof(CompiledQuery_ConstantUpdateBody));

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Description"" = 'Updated', ""Price"" = '1.5'
WHERE ""i"".""ItemId"" <= 388
");
        }

        public override void CompiledQuery_HasOwnedType()
        {
            base.CompiledQuery_HasOwnedType();

            LogSql(nameof(CompiledQuery_HasOwnedType));

            AssertSql(@"
UPDATE ""ChangeLog_{{schema}}"" AS ""c""
SET ""Audit_IsDeleted"" = NOT (""c"".""Audit_IsDeleted"")
");
        }

        public override void CompiledQuery_NavigationSelect()
        {
            base.CompiledQuery_NavigationSelect();

            LogSql(nameof(CompiledQuery_NavigationSelect));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = (""d"".""Another"" + ""j"".""SubmissionId"") + @__x
FROM ""Judging_{{schema}}"" AS ""j""
WHERE ""d"".""JudgingId"" = ""j"".""JudgingId""
");
        }

        public override void CompiledQuery_NavigationWhere()
        {
            base.CompiledQuery_NavigationWhere();

            LogSql(nameof(CompiledQuery_NavigationWhere));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = ""j"".""SubmissionId""
FROM ""Judging_{{schema}}"" AS ""j""
WHERE (""j"".""PreviousJudgingId"" = @__x) AND (""d"".""JudgingId"" = ""j"".""JudgingId"")
");
        }

        public override void CompiledQuery_ParameterUpdateBody()
        {
            base.CompiledQuery_ParameterUpdateBody();

            LogSql(nameof(CompiledQuery_ParameterUpdateBody));

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Description"" = @__desc, ""Price"" = @__pri
WHERE ""i"".""ItemId"" <= 388
");
        }

        public override void CompiledQuery_ScalarSubquery()
        {
            base.CompiledQuery_ScalarSubquery();

            LogSql(nameof(CompiledQuery_ScalarSubquery));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = (
    SELECT COUNT(*)
    FROM ""Item_{{schema}}"" AS ""i"")
");
        }

        public override void CompiledQuery_SetNull()
        {
            base.CompiledQuery_SetNull();

            LogSql(nameof(CompiledQuery_SetNull));

            AssertSql(@"
UPDATE ""Judging_{{schema}}"" AS ""j""
SET ""CompileError"" = NULL, ""ExecuteMemory"" = NULL, ""PreviousJudgingId"" = NULL, ""TotalScore"" = NULL, ""Server"" = NULL, ""Status"" = max(""j"".""Status"", 8)
");
        }

        public override void ConcatenateBody()
        {
            base.ConcatenateBody();

            LogSql(nameof(ConcatenateBody));

            AssertSql31(@"
UPDATE ""Item_{{schema}}"" AS i
SET ""Name"" = ""i"".""Name"" || @__suffix_1, ""Quantity"" = ""i"".""Quantity"" + @__incrementStep_2
WHERE (""i"".""ItemId"" <= 500) AND (ef_compare(""i"".""Price"", @__price_0) >= 0)
");

            AssertSql50(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Name"" = COALESCE(""i"".""Name"", '') || @__suffix_1, ""Quantity"" = ""i"".""Quantity"" + @__incrementStep_2
WHERE (""i"".""ItemId"" <= 500) AND (ef_compare(""i"".""Price"", @__price_0) >= 0)
");
        }

        public override void ConstantUpdateBody()
        {
            base.ConstantUpdateBody();

            LogSql(nameof(ConstantUpdateBody));

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Description"" = 'Updated', ""Price"" = '1.5'
WHERE (""i"".""ItemId"" <= 388) AND (ef_compare(""i"".""Price"", @__price_0) >= 0)
");
        }

        public override void HasOwnedType()
        {
            base.HasOwnedType();

            LogSql(nameof(HasOwnedType));

            AssertSql(@"
UPDATE ""ChangeLog_{{schema}}"" AS ""c""
SET ""Audit_IsDeleted"" = NOT (""c"".""Audit_IsDeleted"")
");
        }

        public override void NavigationSelect()
        {
            base.NavigationSelect();

            LogSql(nameof(NavigationSelect));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = (""d"".""Another"" + ""j"".""SubmissionId"") + @__x_0
FROM ""Judging_{{schema}}"" AS ""j""
WHERE ""d"".""JudgingId"" = ""j"".""JudgingId""
");
        }

        public override void NavigationWhere()
        {
            base.NavigationWhere();

            LogSql(nameof(NavigationWhere));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = ""j"".""SubmissionId""
FROM ""Judging_{{schema}}"" AS ""j""
WHERE (""j"".""PreviousJudgingId"" = @__x_0) AND (""d"".""JudgingId"" = ""j"".""JudgingId"")
");
        }

        public override void ParameterUpdateBody()
        {
            base.ParameterUpdateBody();

            LogSql(nameof(ParameterUpdateBody));

            AssertSql(@"
UPDATE ""Item_{{schema}}"" AS ""i""
SET ""Description"" = @__desc_1, ""Price"" = @__pri_2
WHERE (""i"".""ItemId"" <= 388) AND (ef_compare(""i"".""Price"", @__price_0) >= 0)
");
        }

        public override void ScalarSubquery()
        {
            base.ScalarSubquery();

            LogSql(nameof(ScalarSubquery));

            AssertSql(@"
UPDATE ""Detail_{{schema}}"" AS ""d""
SET ""Another"" = (
    SELECT COUNT(*)
    FROM ""Item_{{schema}}"" AS ""i"")
");
        }

        public override void SetNull()
        {
            Assert.Throws<InvalidOperationException>(() => base.SetNull());
        }
    }
}
