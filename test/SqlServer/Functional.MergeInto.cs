using Microsoft.EntityFrameworkCore.Tests.MergeInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerMergeIntoTest : MergeIntoTestBase<SqlServerContextFactory<MergeContext>>
    {
        public SqlServerMergeIntoTest(
            SqlServerContextFactory<MergeContext> factory)
            : base(factory)
        {
        }
    }
}
