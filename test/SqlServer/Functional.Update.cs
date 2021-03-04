using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUpdateTest : UpdateTestBase<SqlServerContextFactory<UpdateContext>>
    {
        public SqlServerUpdateTest(
            SqlServerContextFactory<UpdateContext> factory,
            DataFixture<SqlServerContextFactory<UpdateContext>> dataFixture)
            : base(factory, dataFixture)
        {
        }
    }
}
