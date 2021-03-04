using Microsoft.EntityFrameworkCore.Tests.MergeInto;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryMergeIntoTest : MergeIntoTestBase<InMemoryContextFactory<MergeContext>>
    {
        public InMemoryMergeIntoTest(
            InMemoryContextFactory<MergeContext> factory)
            : base(factory)
        {
        }
    }
}
