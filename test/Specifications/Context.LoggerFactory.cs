using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public static class ContextLoggerFactory
    {
        public static ILoggerFactory Singleton { get; } = LoggerFactory.Create(l => l.AddConsole().AddDebug());
    }
}
