using Microsoft.EntityFrameworkCore.Tests.MergeInto;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerMergeIntoTest : MergeIntoTestBase<SqlServerContextFactory<MergeContext>>
    {
        protected SqlServerMergeIntoTest(
            SqlServerContextFactory<MergeContext> factory)
            : base(factory)
        {
        }

        protected override IQueryable<RankSource> GetSqlRawForRankSource()
        {
            throw new System.NotImplementedException();
        }
    }
}
