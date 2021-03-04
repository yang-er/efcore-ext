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
    }
}
