using Microsoft.EntityFrameworkCore.Tests.BatchUpdate;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryUpdateTest : UpdateTestBase<InMemoryContextFactory<UpdateContext>>
    {
        public InMemoryUpdateTest(
            InMemoryContextFactory<UpdateContext> factory)
            : base(factory)
        {
        }
    }
}
