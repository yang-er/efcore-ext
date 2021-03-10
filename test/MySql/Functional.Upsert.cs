using Microsoft.EntityFrameworkCore.Tests.Upsert;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUpsertTest : UpsertTestBase<MySqlContextFactory<UpsertContext>>
    {
        public MySqlUpsertTest(
            MySqlContextFactory<UpsertContext> factory)
            : base(factory)
        {
        }

        public override void InsertIfNotExistOne()
        {
            base.InsertIfNotExistOne();

            LogSql(nameof(InsertIfNotExistOne));

            AssertSql(@"

");
        }

        public override void InsertIfNotExistOne_CompiledQuery()
        {
            base.InsertIfNotExistOne_CompiledQuery();

            LogSql(nameof(InsertIfNotExistOne_CompiledQuery));

            AssertSql(@"

");
        }

        public override void InsertIfNotExists_AnotherTable()
        {
            base.InsertIfNotExists_AnotherTable();

            LogSql(nameof(InsertIfNotExists_AnotherTable));

            AssertSql(@"

");
        }

        public override void InsertIfNotExists_SubSelect_CompiledQuery()
        {
            base.InsertIfNotExists_SubSelect_CompiledQuery();

            LogSql(nameof(InsertIfNotExists_SubSelect_CompiledQuery));

            AssertSql(@"

");
        }

        public override void Translation_Parameterize()
        {
            base.Translation_Parameterize();

            LogSql(nameof(Translation_Parameterize));

            AssertSql(@"

");
        }

        public override void Upsert_AlternativeKey()
        {
            base.Upsert_AlternativeKey();

            LogSql(nameof(Upsert_AlternativeKey));

            AssertSql(@"

");
        }

        public override void Upsert_AnotherTable()
        {
            base.Upsert_AnotherTable();

            LogSql(nameof(Upsert_AnotherTable));

            AssertSql(@"

");
        }

        public override void Upsert_FromSql()
        {
            base.Upsert_FromSql();

            LogSql(nameof(Upsert_FromSql));

            AssertSql(@"

");
        }

        public override void Upsert_NewAnonymousObject()
        {
            base.Upsert_NewAnonymousObject();

            LogSql(nameof(Upsert_NewAnonymousObject));

            AssertSql(@"

");
        }

        public override void Upsert_NewAnonymousObject_CompiledQuery()
        {
            base.Upsert_NewAnonymousObject_CompiledQuery();

            LogSql(nameof(Upsert_NewAnonymousObject_CompiledQuery));

            AssertSql(@"

");
        }

        public override void Upsert_SubSelect()
        {
            base.Upsert_SubSelect();

            LogSql(nameof(Upsert_SubSelect));

            AssertSql(@"

");
        }

        public override void UpsertOne()
        {
            base.UpsertOne();

            LogSql(nameof(UpsertOne));

            AssertSql(@"

");
        }

        public override void UpsertOne_CompiledQuery()
        {
            base.UpsertOne_CompiledQuery();

            LogSql(nameof(UpsertOne_CompiledQuery));

            AssertSql(@"

");
        }
    }
}
