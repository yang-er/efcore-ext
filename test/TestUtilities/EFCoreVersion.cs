using System;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [Flags]
    public enum EFCoreVersion
    {
        None = 0,
        Version_3_1 = 1 << 0,
        Version_5_0 = 1 << 1,
    }
}
