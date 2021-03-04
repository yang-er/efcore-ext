using Microsoft.EntityFrameworkCore.Tests.MergeInto;
using System.Linq;

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
