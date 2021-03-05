using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerInsertIntoTest : InsertIntoTestBase<SqlServerContextFactory<SelectIntoContext>>
    {
        public SqlServerInsertIntoTest(
            SqlServerContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalSelectInto()
        {
            base.CompiledQuery_NormalSelectInto();

            LogSql(nameof(CompiledQuery_NormalSelectInto));

            AssertSql(@"
INSERT INTO [ChangeLog_{{schema}}] ([Description], [ChangedBy], [Audit_IsDeleted])
SELECT COALESCE([j].[Server], @__hh) + N'666' AS [Description], [j].[CompileError] AS [ChangedBy], CAST(1 AS bit) AS [Audit_IsDeleted]
FROM [Judging_{{schema}}] AS [j]
");
        }

        public override void CompiledQuery_WithAbstractType()
        {
            base.CompiledQuery_WithAbstractType();

            LogSql(nameof(CompiledQuery_WithAbstractType));

            AssertSql(@"
INSERT INTO [Person_{{schema}}] ([Name], [Class])
SELECT [p].[Name], [p].[Subject] AS [Class]
FROM [Person_{{schema}}] AS [p]
WHERE [p].[Discriminator] = N'Student'
");
        }

        public override void CompiledQuery_WithComputedColumn()
        {
            base.CompiledQuery_WithComputedColumn();

            LogSql(nameof(CompiledQuery_WithComputedColumn));

            AssertSql31(@"
INSERT INTO [Document_{{schema}}] ([Content])
SELECT [d].[Content] + CONVERT(VARCHAR(11), [d].[ContentLength]) AS [Content]
FROM [Document_{{schema}}] AS [d]
");

            AssertSql50(@"
INSERT INTO [Document_{{schema}}] ([Content])
SELECT [d].[Content] + COALESCE(CONVERT(VARCHAR(11), [d].[ContentLength]), N'') AS [Content]
FROM [Document_{{schema}}] AS [d]
");
        }

        public override void NormalSelectInto()
        {
            base.NormalSelectInto();

            LogSql(nameof(NormalSelectInto));

            AssertSql(@"
INSERT INTO [ChangeLog_{{schema}}] ([Description], [ChangedBy], [Audit_IsDeleted])
SELECT COALESCE([j].[Server], @__hh_0) + N'666' AS [Description], [j].[CompileError] AS [ChangedBy], CAST(1 AS bit) AS [Audit_IsDeleted]
FROM [Judging_{{schema}}] AS [j]
");
        }

        public override void WithAbstractType()
        {
            base.WithAbstractType();

            LogSql(nameof(WithAbstractType));

            AssertSql(@"
INSERT INTO [Person_{{schema}}] ([Name], [Class])
SELECT [p].[Name], [p].[Subject] AS [Class]
FROM [Person_{{schema}}] AS [p]
WHERE [p].[Discriminator] = N'Student'
");
        }

        public override void WithComputedColumn()
        {
            base.WithComputedColumn();

            LogSql(nameof(WithComputedColumn));

            AssertSql31(@"
INSERT INTO [Document_{{schema}}] ([Content])
SELECT [d].[Content] + CONVERT(VARCHAR(11), [d].[ContentLength]) AS [Content]
FROM [Document_{{schema}}] AS [d]
");

            AssertSql50(@"
INSERT INTO [Document_{{schema}}] ([Content])
SELECT [d].[Content] + COALESCE(CONVERT(VARCHAR(11), [d].[ContentLength]), N'') AS [Content]
FROM [Document_{{schema}}] AS [d]
");
        }
    }
}
