using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class ContextLoggerFactory
    {
        public ILoggerFactory Instance { get; }

        public ContextLoggerFactory()
        {
            Instance = LoggerFactory.Create(l => l.AddConsole().AddDebug());
        }
    }
}
