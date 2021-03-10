using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlInsertIntoTest : InsertIntoTestBase<MySqlContextFactory<SelectIntoContext>>
    {
        public MySqlInsertIntoTest(
            MySqlContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_NormalSelectInto()
        {
            base.CompiledQuery_NormalSelectInto();

            LogSql(nameof(CompiledQuery_NormalSelectInto));

            AssertSql(@"

");
        }

        public override void CompiledQuery_WithAbstractType()
        {
            base.CompiledQuery_WithAbstractType();

            LogSql(nameof(CompiledQuery_WithAbstractType));

            AssertSql(@"

");
        }

        public override void CompiledQuery_WithComputedColumn()
        {
            base.CompiledQuery_WithComputedColumn();

            LogSql(nameof(CompiledQuery_WithComputedColumn));

            AssertSql(@"

");
        }

        public override void NormalSelectInto()
        {
            base.NormalSelectInto();

            LogSql(nameof(NormalSelectInto));

            AssertSql(@"

");
        }

        public override void WithAbstractType()
        {
            base.WithAbstractType();

            LogSql(nameof(WithAbstractType));

            AssertSql(@"

");
        }

        public override void WithComputedColumn()
        {
            base.WithComputedColumn();

            LogSql(nameof(WithComputedColumn));

            AssertSql(@"

");
        }
    }
}
