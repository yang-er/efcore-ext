using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class CommandTracer : ICommandNotifier
    {
        private bool _receivingCommand;

        string ICommandNotifier.LastCommand { get; set; }

        bool ICommandNotifier.Receiving => _receivingCommand;

        public void AssertSql(string sql)
        {
            sql = sql.Replace("\r", string.Empty).Replace("\n", Environment.NewLine);
            Assert.Equal(sql, ((ICommandNotifier)this).LastCommand);
        }

        public CommandReceivingDisposable BeginScope()
        {
            if (_receivingCommand)
            {
                throw new InvalidOperationException("Cannot create a nested scope.");
            }

            return new CommandReceivingDisposable(this);
        }

        public class CommandReceivingDisposable : IDisposable
        {
            private CommandTracer _query;

            public CommandReceivingDisposable(CommandTracer query)
            {
                query._receivingCommand = true;
                _query = query;
            }

            public void Dispose()
            {
                if (_query != null) _query._receivingCommand = false;
                _query = null;
            }
        }
    }
}
