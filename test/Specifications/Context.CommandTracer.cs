using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class CommandTracer : ICommandNotifier
    {
        private string _lastCommand;
        private bool _receivingCommand;

        string ICommandNotifier.LastCommand
        {
            get
            {
                return _lastCommand;
            }

            set
            {
                _lastCommand = value;
                Output.WriteLine(
                    "------------------------------------------\n" +
                    $"-- {DateTimeOffset.UtcNow:s} Received Command\n" +
                    "------------------------------------------\n" +
                    (value ?? "<null>") +
                    "\n");
            }
        }

        bool ICommandNotifier.Receiving => _receivingCommand;

        public void AssertSql(string sql)
        {
            sql = sql.Trim().Replace("\r", string.Empty).Replace("\n", Environment.NewLine);
            Assert.Equal(sql, _lastCommand);
        }

        public CommandReceivingDisposable BeginScope()
        {
            if (_receivingCommand)
            {
                throw new InvalidOperationException("Cannot create a nested scope.");
            }

            return new CommandReceivingDisposable(this);
        }

        public sealed class CommandReceivingDisposable : IDisposable
        {
            private CommandTracer _query;

            public CommandReceivingDisposable(CommandTracer query)
            {
                query._receivingCommand = true;
                _query = query;
            }

            public void Dispose()
            {
                if (_query != null)
                {
                    _query._receivingCommand = false;
                }

                _query = null;
            }
        }
    }
}
