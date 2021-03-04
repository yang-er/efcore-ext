using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerInsertIntoTest : InsertIntoTestBase<SqlServerContextFactory<SelectIntoContext>>
    {
        public SqlServerInsertIntoTest(
            SqlServerContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }
    }
}
