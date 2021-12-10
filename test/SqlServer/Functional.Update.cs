using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUpdateTest : UpdateTestBase<SqlServerContextFactory<UpdateContext>>
    {
        public SqlServerUpdateTest(
            SqlServerContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConcatenateBody()
        {
            base.CompiledQuery_ConcatenateBody();

            AssertSql(V31, @"
UPDATE [i]
SET [i].[Name] = [i].[Name] + @__suffix, [i].[Quantity] = [i].[Quantity] + @__incrementStep
FROM [Item_{{schema}}] AS [i]
WHERE [i].[ItemId] <= 500
");

            AssertSql(V50 | V60, @"
UPDATE [i]
SET [i].[Name] = COALESCE([i].[Name], N'') + @__suffix, [i].[Quantity] = [i].[Quantity] + @__incrementStep
FROM [Item_{{schema}}] AS [i]
WHERE [i].[ItemId] <= 500
");
        }

        public override void CompiledQuery_ConstantUpdateBody()
        {
            base.CompiledQuery_ConstantUpdateBody();

            AssertSql(@"
UPDATE [i]
SET [i].[Description] = N'Updated', [i].[Price] = 1.5
FROM [Item_{{schema}}] AS [i]
WHERE [i].[ItemId] <= 388
");
        }

        public override void CompiledQuery_HasOwnedType()
        {
            base.CompiledQuery_HasOwnedType();

            AssertSql(V31, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [t].[Audit_IsDeleted] <> CAST(1 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
LEFT JOIN (
    SELECT [c0].[ChangeLogId], [c0].[ChangedBy], [c0].[Audit_IsDeleted]
    FROM [ChangeLog_{{schema}}] AS [c0]
    WHERE [c0].[Audit_IsDeleted] IS NOT NULL
) AS [t] ON [c].[ChangeLogId] = [t].[ChangeLogId]
");

            AssertSql(V50, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [c].[Audit_IsDeleted] <> CAST(1 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
");

            AssertSql(V60, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [c].[Audit_IsDeleted] = CAST(0 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
");
        }

        public override void CompiledQuery_NavigationSelect()
        {
            base.CompiledQuery_NavigationSelect();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = ([d].[Another] + [j].[SubmissionId]) + @__x
FROM [Detail_{{schema}}] AS [d]
INNER JOIN [Judging_{{schema}}] AS [j] ON [d].[JudgingId] = [j].[JudgingId]
");
        }

        public override void CompiledQuery_NavigationWhere()
        {
            base.CompiledQuery_NavigationWhere();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = [j].[SubmissionId]
FROM [Detail_{{schema}}] AS [d]
INNER JOIN [Judging_{{schema}}] AS [j] ON [d].[JudgingId] = [j].[JudgingId]
WHERE [j].[PreviousJudgingId] = @__x
");
        }

        public override void CompiledQuery_ParameterUpdateBody()
        {
            base.CompiledQuery_ParameterUpdateBody();

            AssertSql(@"
UPDATE [i]
SET [i].[Description] = @__desc, [i].[Price] = @__pri
FROM [Item_{{schema}}] AS [i]
WHERE [i].[ItemId] <= 388
");
        }

        public override void CompiledQuery_ScalarSubquery()
        {
            base.CompiledQuery_ScalarSubquery();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = (
    SELECT COUNT(*)
    FROM [Item_{{schema}}] AS [i])
FROM [Detail_{{schema}}] AS [d]
");
        }

        public override void CompiledQuery_SetNull()
        {
            base.CompiledQuery_SetNull();

            AssertSql(@"
UPDATE [j]
SET [j].[CompileError] = NULL, [j].[ExecuteMemory] = NULL, [j].[PreviousJudgingId] = NULL, [j].[TotalScore] = NULL, [j].[Server] = NULL, [j].[Status] = CASE
    WHEN [j].[Status] > 8 THEN [j].[Status]
    ELSE 8
END
FROM [Judging_{{schema}}] AS [j]
");
        }

        public override void ConcatenateBody()
        {
            base.ConcatenateBody();

            AssertSql(V31, @"
UPDATE [i]
SET [i].[Name] = [i].[Name] + @__suffix_1, [i].[Quantity] = [i].[Quantity] + @__incrementStep_2
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] <= 500) AND ([i].[Price] >= @__price_0)
");

            AssertSql(V50 | V60, @"
UPDATE [i]
SET [i].[Name] = COALESCE([i].[Name], N'') + @__suffix_1, [i].[Quantity] = [i].[Quantity] + @__incrementStep_2
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] <= 500) AND ([i].[Price] >= @__price_0)
");
        }

        public override void ConstantUpdateBody()
        {
            base.ConstantUpdateBody();

            AssertSql(@"
UPDATE [i]
SET [i].[Description] = N'Updated', [i].[Price] = 1.5
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] <= 388) AND ([i].[Price] >= @__price_0)
");
        }

        public override void HasOwnedType()
        {
            base.HasOwnedType();

            AssertSql(V31, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [t].[Audit_IsDeleted] <> CAST(1 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
LEFT JOIN (
    SELECT [c0].[ChangeLogId], [c0].[ChangedBy], [c0].[Audit_IsDeleted]
    FROM [ChangeLog_{{schema}}] AS [c0]
    WHERE [c0].[Audit_IsDeleted] IS NOT NULL
) AS [t] ON [c].[ChangeLogId] = [t].[ChangeLogId]
");

            AssertSql(V50, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [c].[Audit_IsDeleted] <> CAST(1 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
");

            AssertSql(V60, @"
UPDATE [c]
SET [c].[Audit_IsDeleted] = CASE
    WHEN [c].[Audit_IsDeleted] = CAST(0 AS bit) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END
FROM [ChangeLog_{{schema}}] AS [c]
");
        }

        public override void NavigationSelect()
        {
            base.NavigationSelect();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = ([d].[Another] + [j].[SubmissionId]) + @__x_0
FROM [Detail_{{schema}}] AS [d]
INNER JOIN [Judging_{{schema}}] AS [j] ON [d].[JudgingId] = [j].[JudgingId]
");
        }

        public override void NavigationWhere()
        {
            base.NavigationWhere();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = [j].[SubmissionId]
FROM [Detail_{{schema}}] AS [d]
INNER JOIN [Judging_{{schema}}] AS [j] ON [d].[JudgingId] = [j].[JudgingId]
WHERE [j].[PreviousJudgingId] = @__x_0
");
        }

        public override void ParameterUpdateBody()
        {
            base.ParameterUpdateBody();

            AssertSql(@"
UPDATE [i]
SET [i].[Description] = @__desc_1, [i].[Price] = @__pri_2
FROM [Item_{{schema}}] AS [i]
WHERE ([i].[ItemId] <= 388) AND ([i].[Price] >= @__price_0)
");
        }

        public override void ScalarSubquery()
        {
            base.ScalarSubquery();

            AssertSql(@"
UPDATE [d]
SET [d].[Another] = (
    SELECT COUNT(*)
    FROM [Item_{{schema}}] AS [i])
FROM [Detail_{{schema}}] AS [d]
");
        }

        public override void SetNull()
        {
            base.SetNull();

            AssertSql(@"
UPDATE [j]
SET [j].[CompileError] = NULL, [j].[ExecuteMemory] = NULL, [j].[PreviousJudgingId] = NULL, [j].[TotalScore] = NULL, [j].[StartTime] = SYSDATETIMEOFFSET(), [j].[Server] = NULL, [j].[Status] = CASE
    WHEN [j].[Status] > 8 THEN [j].[Status]
    ELSE 8
END
FROM [Judging_{{schema}}] AS [j]
");
        }

        public override void BodyScalarQueryWithWhere()
        {
            base.BodyScalarQueryWithWhere();

            AssertSql(V31 | V50, @"
UPDATE [c0]
SET [c0].[Count] = (
    SELECT COUNT(*)
    FROM [ContestTeam_{{schema}}] AS [c]
    WHERE ([c].[ContestId] = [c0].[Id]) AND ([c].[Status] = @__b_1))
FROM [Contest_{{schema}}] AS [c0]
WHERE [c0].[Id] = @__a_0
");

            AssertSql(V60, @"
UPDATE [c]
SET [c].[Count] = (
    SELECT COUNT(*)
    FROM [ContestTeam_{{schema}}] AS [c0]
    WHERE ([c0].[ContestId] = [c].[Id]) AND ([c0].[Status] = @__b_1))
FROM [Contest_{{schema}}] AS [c]
WHERE [c].[Id] = @__a_0
");
        }
    }
}
