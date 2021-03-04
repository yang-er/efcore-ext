using Microsoft.EntityFrameworkCore.Tests.Upsert;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerUpsertTest : UpsertTestBase<SqlServerContextFactory<UpsertContext>>
    {
        public SqlServerUpsertTest(
            SqlServerContextFactory<UpsertContext> factory)
            : base(factory)
        {
        }
    }
}
