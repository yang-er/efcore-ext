using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class CommandInterceptor : DbCommandInterceptor
    {
        private readonly ICommandNotifier _commandNotifier;

        public CommandInterceptor(ICommandNotifier commandNotifier)
        {
            _commandNotifier = commandNotifier;
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ScalarExecuting(command, eventData, result);
        }

#if EFCORE31

        public override Task<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override Task<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override Task<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

#elif EFCORE50

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            _commandNotifier.SetLastCommand(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

#endif

    }
}
