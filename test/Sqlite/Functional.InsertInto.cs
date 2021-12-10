using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqliteInsertIntoTest : InsertIntoTestBase<SqliteContextFactory<SelectIntoContext>>
    {
        public SqliteInsertIntoTest(
            SqliteContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalSelectInto()
        {
            base.CompiledQuery_NormalSelectInto();

            AssertSql(@"
INSERT INTO ""ChangeLog_{{schema}}"" (""Description"", ""ChangedBy"", ""Audit_IsDeleted"")
SELECT COALESCE(""j"".""Server"", @__hh) || '666' AS ""Description"", ""j"".""CompileError"" AS ""ChangedBy"", 1 AS ""Audit_IsDeleted""
FROM ""Judging_{{schema}}"" AS ""j""
");
        }

        public override void CompiledQuery_WithAbstractType()
        {
            base.CompiledQuery_WithAbstractType();

            AssertSql(@"
INSERT INTO ""Person_{{schema}}"" (""Name"", ""Class"")
SELECT ""p"".""Name"", ""p"".""Subject"" AS ""Class""
FROM ""Person_{{schema}}"" AS ""p""
WHERE ""p"".""Discriminator"" = 'Student'
");
        }

        public override void NormalSelectInto()
        {
            base.NormalSelectInto();

            AssertSql(@"
INSERT INTO ""ChangeLog_{{schema}}"" (""Description"", ""ChangedBy"", ""Audit_IsDeleted"")
SELECT COALESCE(""j"".""Server"", @__hh_0) || '666' AS ""Description"", ""j"".""CompileError"" AS ""ChangedBy"", 1 AS ""Audit_IsDeleted""
FROM ""Judging_{{schema}}"" AS ""j""
");
        }

        public override void WithAbstractType()
        {
            base.WithAbstractType();

            AssertSql(@"
INSERT INTO ""Person_{{schema}}"" (""Name"", ""Class"")
SELECT ""p"".""Name"", ""p"".""Subject"" AS ""Class""
FROM ""Person_{{schema}}"" AS ""p""
WHERE ""p"".""Discriminator"" = 'Student'
");
        }
    }
}
