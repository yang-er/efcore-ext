using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlUpdateTest : UpdateTestBase<NpgsqlContextFactory<UpdateContext>>
    {
        public NpgsqlUpdateTest(
            NpgsqlContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }
    }
}
