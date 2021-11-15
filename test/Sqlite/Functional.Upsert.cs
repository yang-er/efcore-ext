using Microsoft.EntityFrameworkCore.Tests.Upsert;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqliteUpsertTest : UpsertTestBase<SqliteContextFactory<UpsertContext>>
    {
        public SqliteUpsertTest(
            SqliteContextFactory<UpsertContext> factory)
            : base(factory)
        {
        }

        public override void InsertIfNotExistOne()
        {
            base.InsertIfNotExistOne();

            LogSql(nameof(InsertIfNotExistOne));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
VALUES (1, 1, @__time_0, @__time_0, @__cid_1, @__teamid_2)
ON CONFLICT DO NOTHING
");
        }

        public override void InsertIfNotExistOne_CompiledQuery()
        {
            base.InsertIfNotExistOne_CompiledQuery();

            LogSql(nameof(InsertIfNotExistOne_CompiledQuery));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""ContestId"", ""TeamId"", ""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"")
VALUES (@__cid, @__teamid, 1, 1, @__time, @__time)
ON CONFLICT DO NOTHING
");
        }

        public override void InsertIfNotExists_AnotherTable()
        {
            base.InsertIfNotExists_AnotherTable();

            LogSql(nameof(InsertIfNotExists_AnotherTable));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""r0"".""Time"", ""r0"".""Time"", ""r0"".""ContestId"", ""r0"".""TeamId""
FROM ""RankSource_{{schema}}"" AS ""r0"" WHERE TRUE
ON CONFLICT DO NOTHING
");
        }

        public override void InsertIfNotExists_SubSelect_CompiledQuery()
        {
            base.InsertIfNotExists_SubSelect_CompiledQuery();

            LogSql(nameof(InsertIfNotExists_SubSelect_CompiledQuery));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""t"".""Time"", ""t"".""Time"", ""t"".""ContestId"", ""t"".""TeamId""
FROM (
    SELECT DISTINCT ""r0"".""ContestId"", ""r0"".""TeamId"", ""r0"".""Public"", ""r0"".""Time""
    FROM ""RankSource_{{schema}}"" AS ""r0""
) AS ""t"" WHERE TRUE
ON CONFLICT DO NOTHING
");
        }

        public override void Translation_Parameterize()
        {
            base.Translation_Parameterize();

            LogSql(nameof(Translation_Parameterize));

            AssertSql(@"
INSERT INTO ""TwoRelation_{{schema}}"" AS ""t"" (""BbbId"", ""AaaId"")
VALUES (@__bbb_1, @__aaa_2)
ON CONFLICT DO NOTHING
");
        }

        public override void Upsert_AlternativeKey()
        {
            base.Upsert_AlternativeKey();

            LogSql(nameof(Upsert_AlternativeKey));

            AssertSql(@"
INSERT INTO ""ThreeRelation_{{schema}}"" AS ""t"" (""BbbId"", ""AaaId"")
VALUES (@__p_0_0_1, @__p_0_0_0)
ON CONFLICT DO NOTHING
");
        }

        public override void Upsert_AnotherTable()
        {
            base.Upsert_AnotherTable();

            LogSql(nameof(Upsert_AnotherTable));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""r0"".""Time"", ""r0"".""Time"", ""r0"".""ContestId"", ""r0"".""TeamId""
FROM ""RankSource_{{schema}}"" AS ""r0"" WHERE TRUE
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");
        }

        public override void Upsert_FromSql()
        {
            base.Upsert_FromSql();

            LogSql(nameof(Upsert_FromSql));

            AssertSql(V31 | V50, @"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""r0"".""Time"", ""r0"".""Time"", ""r0"".""ContestId"", ""r0"".""TeamId""
FROM (
    SELECT ""r"".""ContestId"", ""r"".""TeamId"", ""r"".""Public"", ""r"".""Time""
    FROM ""RankSource_{{schema}}"" AS ""r""
) AS ""r0"" WHERE TRUE
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");

            AssertSql(V60, @"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""m"".""Time"", ""m"".""Time"", ""m"".""ContestId"", ""m"".""TeamId""
FROM (
    SELECT ""r"".""ContestId"", ""r"".""TeamId"", ""r"".""Public"", ""r"".""Time""
    FROM ""RankSource_{{schema}}"" AS ""r""
) AS ""m"" WHERE TRUE
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");
        }

        public override void Upsert_NewAnonymousObject()
        {
            base.Upsert_NewAnonymousObject();

            LogSql(nameof(Upsert_NewAnonymousObject));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
VALUES (1, 1, @__p_0_0_2, @__p_0_0_2, @__p_0_0_0, @__p_0_0_1),
(1, 1, @__p_0_1_2, @__p_0_1_2, @__p_0_1_0, @__p_0_1_1)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");
        }

        public override void Upsert_NewAnonymousObject_CompiledQuery()
        {
            base.Upsert_NewAnonymousObject_CompiledQuery();

            LogSql(nameof(Upsert_NewAnonymousObject_CompiledQuery));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
VALUES (1, 1, @__time1, @__time1, 1, 2),
(1, 1, 50, 50, 3, @__teamid2)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");
        }

        public override void Upsert_SubSelect()
        {
            base.Upsert_SubSelect();

            LogSql(nameof(Upsert_SubSelect));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
SELECT 1, 1, ""t"".""Time"", ""t"".""Time"", ""t"".""ContestId"", ""t"".""TeamId""
FROM (
    SELECT DISTINCT ""r0"".""ContestId"", ""r0"".""TeamId"", ""r0"".""Public"", ""r0"".""Time""
    FROM ""RankSource_{{schema}}"" AS ""r0""
) AS ""t"" WHERE TRUE
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + ""excluded"".""TotalTimePublic""
");
        }

        public override void UpsertOne()
        {
            base.UpsertOne();

            LogSql(nameof(UpsertOne));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"", ""ContestId"", ""TeamId"")
VALUES (1, 1, @__time_0, @__time_0, @__cid_1, @__teamid_2)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + @__time_0
");
        }

        public override void UpsertOne_CompiledQuery()
        {
            base.UpsertOne_CompiledQuery();

            LogSql(nameof(UpsertOne_CompiledQuery));

            AssertSql(@"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""ContestId"", ""TeamId"", ""PointsPublic"", ""PointsRestricted"", ""TotalTimePublic"", ""TotalTimeRestricted"")
VALUES (@__cid, @__teamid, 1, 1, @__time, @__time)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""PointsPublic"" = ""r"".""PointsPublic"" + 1, ""TotalTimePublic"" = ""r"".""TotalTimePublic"" + @__time
");
        }

        protected override string Issue6Test0 => @"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""ContestId"", ""TeamId"", ""TotalTimePublic"", ""PointsPublic"", ""PointsRestricted"", ""TotalTimeRestricted"")
SELECT NULL, NULL, NULL, NULL, NULL, NULL WHERE FALSE
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""TotalTimePublic"" = ""excluded"".""TotalTimePublic""
";

        protected override string Issue6Test1 => @"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""ContestId"", ""TeamId"", ""TotalTimePublic"", ""PointsPublic"", ""PointsRestricted"", ""TotalTimeRestricted"")
VALUES (@__p_0_0_0, @__p_0_0_1, @__p_0_0_2, 0, 0, 0),
(@__p_0_1_0, @__p_0_1_1, @__p_0_1_2, 0, 0, 0),
(@__p_0_2_0, @__p_0_2_1, @__p_0_2_2, 0, 0, 0),
(@__p_0_3_0, @__p_0_3_1, @__p_0_3_2, 0, 0, 0)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""TotalTimePublic"" = ""excluded"".""TotalTimePublic""
";

        protected override string Issue6Test2 => @"
INSERT INTO ""RankCache_{{schema}}"" AS ""r"" (""ContestId"", ""TeamId"", ""TotalTimePublic"", ""PointsPublic"", ""PointsRestricted"", ""TotalTimeRestricted"")
VALUES (@__p_0_0_0, @__p_0_0_1, @__p_0_0_2, 0, 0, 0),
(@__p_0_1_0, @__p_0_1_1, @__p_0_1_2, 0, 0, 0)
ON CONFLICT (""ContestId"", ""TeamId"") DO UPDATE SET ""TotalTimePublic"" = ""excluded"".""TotalTimePublic""
";
    }
}
