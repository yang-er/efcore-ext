using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlUpdateTest : UpdateTestBase<MySqlContextFactory<UpdateContext>>
    {
        public MySqlUpdateTest(
            MySqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }

        public override void CompiledQuery_ConcatenateBody()
        {
            base.CompiledQuery_ConcatenateBody();

            LogSql(nameof(CompiledQuery_ConcatenateBody));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void CompiledQuery_ConstantUpdateBody()
        {
            base.CompiledQuery_ConstantUpdateBody();

            LogSql(nameof(CompiledQuery_ConstantUpdateBody));

            AssertSql(@"

");
        }

        public override void CompiledQuery_HasOwnedType()
        {
            base.CompiledQuery_HasOwnedType();

            LogSql(nameof(CompiledQuery_HasOwnedType));

            AssertSql(@"

");
        }

        public override void CompiledQuery_NavigationSelect()
        {
            base.CompiledQuery_NavigationSelect();

            LogSql(nameof(CompiledQuery_NavigationSelect));

            AssertSql(@"

");
        }

        public override void CompiledQuery_NavigationWhere()
        {
            base.CompiledQuery_NavigationWhere();

            LogSql(nameof(CompiledQuery_NavigationWhere));

            AssertSql(@"

");
        }

        public override void CompiledQuery_ParameterUpdateBody()
        {
            base.CompiledQuery_ParameterUpdateBody();

            LogSql(nameof(CompiledQuery_ParameterUpdateBody));

            AssertSql(@"

");
        }

        public override void CompiledQuery_ScalarSubquery()
        {
            base.CompiledQuery_ScalarSubquery();

            LogSql(nameof(CompiledQuery_ScalarSubquery));

            AssertSql(@"

");
        }

        public override void CompiledQuery_SetNull()
        {
            base.CompiledQuery_SetNull();

            LogSql(nameof(CompiledQuery_SetNull));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void ConcatenateBody()
        {
            base.ConcatenateBody();

            LogSql(nameof(ConcatenateBody));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }

        public override void ConstantUpdateBody()
        {
            base.ConstantUpdateBody();

            LogSql(nameof(ConstantUpdateBody));

            AssertSql(@"

");
        }

        public override void HasOwnedType()
        {
            base.HasOwnedType();

            LogSql(nameof(HasOwnedType));

            AssertSql(@"

");
        }

        public override void NavigationSelect()
        {
            base.NavigationSelect();

            LogSql(nameof(NavigationSelect));

            AssertSql(@"

");
        }

        public override void NavigationWhere()
        {
            base.NavigationWhere();

            LogSql(nameof(NavigationWhere));

            AssertSql(@"

");
        }

        public override void ParameterUpdateBody()
        {
            base.ParameterUpdateBody();

            LogSql(nameof(ParameterUpdateBody));

            AssertSql(@"

");
        }

        public override void ScalarSubquery()
        {
            base.ScalarSubquery();

            LogSql(nameof(ScalarSubquery));

            AssertSql(@"

");
        }

        public override void SetNull()
        {
            base.SetNull();

            LogSql(nameof(SetNull));

            AssertSql31(@"

");

            AssertSql50(@"

");
        }
    }
}
