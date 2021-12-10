using Microsoft.EntityFrameworkCore.Tests.MergeInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerMergeIntoTest : MergeIntoTestBase<SqlServerContextFactory<MergeContext>>
    {
        public SqlServerMergeIntoTest(
            SqlServerContextFactory<MergeContext> factory)
            : base(factory)
        {
        }

        public override void SourceFromSql()
        {
            base.SourceFromSql();

            AssertSql(V31 | V50, @"
MERGE INTO [RankCache_{{schema}}] AS [r]
USING (
    SELECT [r].[ContestId], [r].[TeamId], [r].[Public], [r].[Time]
    FROM [RankSource_{{schema}}] AS [r]
) AS [r0]
    ON ([r].[ContestId] = [r0].[ContestId]) AND ([r].[TeamId] = [r0].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[PointsPublic] = CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r].[PointsPublic] + 1
        ELSE [r].[PointsPublic]
    END, [r].[TotalTimePublic] = CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r].[TotalTimePublic] + [r0].[Time]
        ELSE [r].[TotalTimePublic]
    END, [r].[PointsRestricted] = [r].[PointsRestricted] + 1, [r].[TotalTimeRestricted] = [r].[TotalTimeRestricted] + [r0].[Time]
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([PointsPublic], [PointsRestricted], [TotalTimePublic], [TotalTimeRestricted], [ContestId], [TeamId]) VALUES (CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN 1
        ELSE 0
    END, 1, CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r0].[Time]
        ELSE 0
    END, [r0].[Time], [r0].[ContestId], [r0].[TeamId])
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");

            AssertSql(V60, @"
MERGE INTO [RankCache_{{schema}}] AS [r]
USING (
    SELECT [r].[ContestId], [r].[TeamId], [r].[Public], [r].[Time]
    FROM [RankSource_{{schema}}] AS [r]
) AS [m]
    ON ([r].[ContestId] = [m].[ContestId]) AND ([r].[TeamId] = [m].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[PointsPublic] = CASE
        WHEN [m].[Public] = CAST(1 AS bit) THEN [r].[PointsPublic] + 1
        ELSE [r].[PointsPublic]
    END, [r].[TotalTimePublic] = CASE
        WHEN [m].[Public] = CAST(1 AS bit) THEN [r].[TotalTimePublic] + [m].[Time]
        ELSE [r].[TotalTimePublic]
    END, [r].[PointsRestricted] = [r].[PointsRestricted] + 1, [r].[TotalTimeRestricted] = [r].[TotalTimeRestricted] + [m].[Time]
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([PointsPublic], [PointsRestricted], [TotalTimePublic], [TotalTimeRestricted], [ContestId], [TeamId]) VALUES (CASE
        WHEN [m].[Public] = CAST(1 AS bit) THEN 1
        ELSE 0
    END, 1, CASE
        WHEN [m].[Public] = CAST(1 AS bit) THEN [m].[Time]
        ELSE 0
    END, [m].[Time], [m].[ContestId], [m].[TeamId])
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");
        }

        public override void Synchronize()
        {
            base.Synchronize();

            AssertSql(@"
MERGE INTO [RankCache_{{schema}}] AS [r]
USING [RankSource_{{schema}}] AS [r0]
    ON ([r].[ContestId] = [r0].[ContestId]) AND ([r].[TeamId] = [r0].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[PointsPublic] = CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r].[PointsPublic] + 1
        ELSE [r].[PointsPublic]
    END, [r].[TotalTimePublic] = CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r].[TotalTimePublic] + [r0].[Time]
        ELSE [r].[TotalTimePublic]
    END, [r].[PointsRestricted] = [r].[PointsRestricted] + 1, [r].[TotalTimeRestricted] = [r].[TotalTimeRestricted] + [r0].[Time]
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([PointsPublic], [PointsRestricted], [TotalTimePublic], [TotalTimeRestricted], [ContestId], [TeamId]) VALUES (CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN 1
        ELSE 0
    END, 1, CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r0].[Time]
        ELSE 0
    END, [r0].[Time], [r0].[ContestId], [r0].[TeamId])
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");
        }

        public override void Synchronize_LocalTable()
        {
            base.Synchronize_LocalTable();

            AssertSql(@"
MERGE INTO [RankSource_{{schema}}] AS [r]
USING (
    VALUES
    (@__p_0_0_0, @__p_0_0_1),
    (@__p_0_1_0, @__p_0_1_1)
) AS [cte] ([ContestId], [TeamId])
    ON ([r].[ContestId] = [cte].[ContestId]) AND ([r].[TeamId] = [cte].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[Time] = 536
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([Time], [ContestId], [TeamId], [Public]) VALUES (366, [cte].[ContestId], [cte].[TeamId], CAST(1 AS bit))
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");
        }

        public override void Synchronize_LocalTable_Compiled()
        {
            base.Synchronize_LocalTable_Compiled();

            AssertSql(@"
MERGE INTO [RankSource_{{schema}}] AS [r]
USING (
    VALUES
    (1, @__teamid1),
    (@__cid2, @__teamid2)
) AS [cte] ([ContestId], [TeamId])
    ON ([r].[ContestId] = [cte].[ContestId]) AND ([r].[TeamId] = [cte].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[Time] = 536
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");
        }

        public override void Synchronize_RemoteTable_Compiled()
        {
            base.Synchronize_RemoteTable_Compiled();

            AssertSql(@"
MERGE INTO [RankCache_{{schema}}] AS [r]
USING [RankSource_{{schema}}] AS [r0]
    ON ([r].[ContestId] = [r0].[ContestId]) AND ([r].[TeamId] = [r0].[TeamId])
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([PointsPublic], [PointsRestricted], [TotalTimePublic], [TotalTimeRestricted], [ContestId], [TeamId]) VALUES (CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN 1
        ELSE 0
    END, 1, CASE
        WHEN [r0].[Public] = CAST(1 AS bit) THEN [r0].[Time]
        ELSE 0
    END, [r0].[Time], [r0].[ContestId], [r0].[TeamId])
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
");
        }

        public override void Upsert()
        {
            base.Upsert();

            AssertSql(@"
MERGE INTO [RankCache_{{schema}}] AS [r]
USING (
    VALUES
    (@__p_0_0_0, @__p_0_0_1, @__p_0_0_2),
    (@__p_0_1_0, @__p_0_1_1, @__p_0_1_2)
) AS [cte] ([ContestId], [TeamId], [Time])
    ON ([r].[ContestId] = [cte].[ContestId]) AND ([r].[TeamId] = [cte].[TeamId])
WHEN MATCHED
    THEN UPDATE SET [r].[PointsPublic] = [r].[PointsPublic] + 1, [r].[TotalTimePublic] = [r].[TotalTimePublic] + [cte].[Time]
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([PointsPublic], [PointsRestricted], [TotalTimePublic], [TotalTimeRestricted], [ContestId], [TeamId]) VALUES (1, 1, [cte].[Time], [cte].[Time], [cte].[ContestId], [cte].[TeamId]);
");
        }
    }
}
