using Microsoft.EntityFrameworkCore.Tests.BatchDelete;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerDeleteTest : DeleteTestBase<SqlServerContextFactory<DeleteContext>>
    {
        public SqlServerDeleteTest(
            SqlServerContextFactory<DeleteContext> factory,
            DataFixture<SqlServerContextFactory<DeleteContext>> dataFixture)
            : base(factory, dataFixture)
        {
        }
    }
}
