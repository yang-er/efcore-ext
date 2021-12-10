using System.Threading;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    public static class Output
    {
        private static readonly AsyncLocal<ITestOutputHelper> asyncLocal;

        static Output()
        {
            asyncLocal = new AsyncLocal<ITestOutputHelper>();
        }

        internal static void SetInstance(ITestOutputHelper testOutputHelper)
        {
            asyncLocal.Value = testOutputHelper;
        }

        public static void WriteLine(string message)
        {
            asyncLocal.Value?.WriteLine(message);
        }
    }
}
