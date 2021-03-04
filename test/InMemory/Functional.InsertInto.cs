using Microsoft.EntityFrameworkCore.Tests.BatchInsertInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryInsertIntoTest : InsertIntoTestBase<InMemoryContextFactory<SelectIntoContext>>
    {
        public InMemoryInsertIntoTest(
            InMemoryContextFactory<SelectIntoContext> factory)
            : base(factory)
        {
        }
    }
}
