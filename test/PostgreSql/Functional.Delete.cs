﻿using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlDeleteTest : DeleteTestBase<NpgsqlContextFactory<DeleteContext>>
    {
        public NpgsqlDeleteTest(
            NpgsqlContextFactory<DeleteContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConstantCondition()
        {
            base.CompiledQuery_ConstantCondition();

            AssertSql(@"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE (i.""ItemId"" > 500) AND (i.""Price"" = 3.0)
");
        }

        public override void CompiledQuery_ContainsSomething()
        {
            base.CompiledQuery_ContainsSomething();

            AssertSql(V31, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Name"" IN ('jyntnytjyntjntnytnt', 'aaa')
");

            AssertSql(V50, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Name"" = ANY (@__descriptionsToDelete) OR ((i.""Name"" IS NULL) AND (array_position(@__descriptionsToDelete, NULL) IS NOT NULL))
");

            AssertSql(V60, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Name"" = ANY (@__descriptionsToDelete) OR (i.""Name"" IS NULL AND array_position(@__descriptionsToDelete, NULL) IS NOT NULL)
");
        }

        public override void CompiledQuery_ParameteredCondition()
        {
            base.CompiledQuery_ParameteredCondition();

            AssertSql(@"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Name"" = @__nameToDelete
");
        }

        public override void ConstantCondition()
        {
            base.ConstantCondition();

            AssertSql(@"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE (i.""ItemId"" > 500) AND (i.""Price"" = 124.0)
");
        }

        public override void ContainsAndAlsoEqual()
        {
            base.ContainsAndAlsoEqual();

            AssertSql(V31, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" IN ('info') OR (i.""Name"" = @__nameToDelete_1)
");

            AssertSql(V50, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE (i.""Description"" = ANY (@__descriptionsToDelete_0) OR ((i.""Description"" IS NULL) AND (array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL))) OR (i.""Name"" = @__nameToDelete_1)
");

            AssertSql(V60, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE (i.""Description"" = ANY (@__descriptionsToDelete_0) OR (i.""Description"" IS NULL AND array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL)) OR (i.""Name"" = @__nameToDelete_1)
");
        }

        public override void ContainsSomething()
        {
            base.ContainsSomething();

            AssertSql(V31, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" IN ('info', 'aaa')
");

            AssertSql(V50, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR ((i.""Description"" IS NULL) AND (array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL))
");

            AssertSql(V60, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR (i.""Description"" IS NULL AND array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL)
");
        }

        public override void EmptyContains()
        {
            base.EmptyContains();

            AssertSql(V31, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE TRUE = FALSE
");

            AssertSql(V50, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR ((i.""Description"" IS NULL) AND (array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL))
");

            AssertSql(V60, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR (i.""Description"" IS NULL AND array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL)
");
        }

        public override void ListAny()
        {
            base.ListAny();

            AssertSql(V31 | V50, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR ((i.""Description"" IS NULL) AND (array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL))
");

            AssertSql(V60, @"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Description"" = ANY (@__descriptionsToDelete_0) OR (i.""Description"" IS NULL AND array_position(@__descriptionsToDelete_0, NULL) IS NOT NULL)
");
        }

        public override void ParameteredCondition()
        {
            base.ParameteredCondition();

            AssertSql(@"
DELETE
FROM ""Item_{{schema}}"" AS i
WHERE i.""Name"" = @__nameToDelete_0
");
        }
    }
}
