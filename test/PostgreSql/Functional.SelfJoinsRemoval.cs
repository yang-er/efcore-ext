using Microsoft.EntityFrameworkCore.Tests.SelfJoinsRemoval;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUselessJoinsRemovalTest : UselessJoinsRemovalTestBase<NpgsqlContextFactory<TestingContext>>
    {
        public NpgsqlUselessJoinsRemovalTest(
            NpgsqlContextFactory<TestingContext> factory)
            : base(factory)
        {
        }

        public override void GroupJoin_2_3()
        {
            base.GroupJoin_2_3();

            AssertSql(@"
SELECT c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString"", o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq""
FROM ""ChangeLog_{{schema}}"" AS c
LEFT JOIN ""OwnedThree_{{schema}}"" AS o ON c.""ChangeLogId"" = o.""Id""
");
        }

        public override void GroupJoin_3_2()
        {
            base.GroupJoin_3_2();

            AssertSql(@"
SELECT o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq"", c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString""
FROM ""OwnedThree_{{schema}}"" AS o
LEFT JOIN ""ChangeLog_{{schema}}"" AS c ON o.""Id"" = c.""ChangeLogId""
");
        }

        public override void HasOneWithOne_SharedTable()
        {
            base.HasOneWithOne_SharedTable();

            AssertSql(@"
SELECT m.""Id"", m.""What"", m.""Other""
FROM ""MiniInfo_{{schema}}"" AS m
");
        }

        public override void InnerJoin_2_3()
        {
            base.InnerJoin_2_3();

            AssertSql(@"
SELECT c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString"", o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq""
FROM ""ChangeLog_{{schema}}"" AS c
INNER JOIN ""OwnedThree_{{schema}}"" AS o ON c.""ChangeLogId"" = o.""Id""
");
        }

        public override void InnerJoin_3_2()
        {
            base.InnerJoin_3_2();

            AssertSql(@"
SELECT o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq"", c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString""
FROM ""OwnedThree_{{schema}}"" AS o
INNER JOIN ""ChangeLog_{{schema}}"" AS c ON o.""Id"" = c.""ChangeLogId""
");
        }

        public override void Owned()
        {
            base.Owned();

            AssertSql(@"
SELECT c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString""
FROM ""ChangeLog_{{schema}}"" AS c
");
        }

        public override void OwnedThenUnionDistinct()
        {
            base.OwnedThenUnionDistinct();

            AssertSql(V31 | V50, @"
SELECT c.""ChangeLogId"", c.""Description"", c.""ChangedBy"", c.""Audit_IsDeleted"", c.""Audit_What_HelloString""
FROM ""ChangeLog_{{schema}}"" AS c
WHERE ((c.""ChangeLogId"" > 80) OR (c.""ChangeLogId"" < 20)) OR (c.""ChangeLogId"" = 50)
");

            AssertSql(V60, @"
SELECT t0.""ChangeLogId"", t0.""Description"", t0.""ChangedBy"", t0.""Audit_IsDeleted"", t0.""ChangeLogId"", t0.""Audit_What_HelloString""
FROM ""ChangeLog_{{schema}}"" AS t0
WHERE ((t0.""ChangeLogId"" > 80) OR (t0.""ChangeLogId"" < 20)) OR (t0.""ChangeLogId"" = 50)
");
        }

        public override void Owned_SkipTrimming()
        {
            base.Owned_SkipTrimming();

            AssertSql(@"
SELECT n.""Id"", n.""Age"", n.""Name""
FROM ""NormalEntity_{{schema}}"" AS n
INNER JOIN ""NormalEntity_{{schema}}"" AS n0 ON n.""Id"" = n0.""Id""
");
        }

        public override void ReallyJoin()
        {
            base.ReallyJoin();

            AssertSql(@"
SELECT o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq"", o0.""Id"", o0.""Happy"", o0.""Other"", o0.""Apple_AnotherString"", o0.""ChangedBy"", o0.""Apple_Audit_IsDeleted"", o0.""Apple_What_AnotherString"", o0.""Audit_Taq"", o0.""Banana_Taq"", o0.""Banana_AnotherString"", o0.""Cherry_AnotherString"", o0.""Cherry_Taq"", o1.""Id"", o1.""Happy"", o1.""Other"", o1.""Apple_AnotherString"", o1.""ChangedBy"", o1.""Apple_Audit_IsDeleted"", o1.""Apple_What_AnotherString"", o1.""Audit_Taq"", o1.""Banana_Taq"", o1.""Banana_AnotherString"", o1.""Cherry_AnotherString"", o1.""Cherry_Taq""
FROM ""OwnedThree_{{schema}}"" AS o
LEFT JOIN ""OwnedThree_{{schema}}"" AS o0 ON o.""Id"" = o0.""Happy""
INNER JOIN ""OwnedThree_{{schema}}"" AS o1 ON o.""Id"" = o1.""Other""
WHERE o.""Other"" <> 3
");
        }

        public override void ReallyUnionDistinct()
        {
            base.ReallyUnionDistinct();

            AssertSql(@"
SELECT n.""Id"", n.""Name"", n.""Age"", 1 AS ""Type""
FROM ""NormalEntity_{{schema}}"" AS n
WHERE n.""Age"" > 80
UNION
SELECT n0.""Id"", n0.""Name"", n0.""Age"", 2 AS ""Type""
FROM ""NormalEntity_{{schema}}"" AS n0
WHERE n0.""Age"" < 20
UNION
SELECT n1.""Id"", n1.""Name"", n1.""Age"", 3 AS ""Type""
FROM ""NormalEntity_{{schema}}"" AS n1
WHERE n1.""Age"" = 50
");
        }

        public override void ShaperChanged()
        {
            base.ShaperChanged();

            AssertSql(@"
-- hello

SELECT n.""Id"", n.""Age"", n.""Name""
FROM ""NormalEntity_{{schema}}"" AS n
WHERE ((n.""Age"" > 80) OR (n.""Age"" < 20)) OR (n.""Age"" = 50)
");
        }

        public override void SuperOwned()
        {
            base.SuperOwned();

            AssertSql(@"
SELECT o.""Id"", o.""Happy"", o.""Other"", o.""Apple_AnotherString"", o.""ChangedBy"", o.""Apple_Audit_IsDeleted"", o.""Apple_What_AnotherString"", o.""Audit_Taq"", o.""Banana_Taq"", o.""Banana_AnotherString"", o.""Cherry_AnotherString"", o.""Cherry_Taq""
FROM ""OwnedThree_{{schema}}"" AS o
");
        }
    }
}
