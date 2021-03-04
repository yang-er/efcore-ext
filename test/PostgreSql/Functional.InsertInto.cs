using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlInsertIntoTest : InsertIntoTestBase<NpgsqlContextFactory<SelectIntoContext>>
    {
        public NpgsqlInsertIntoTest(
            NpgsqlContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }
    }
}
