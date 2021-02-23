using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class RelationalBulkQueryExecutor : IBulkQueryExecutor
    {
        private readonly RelationalCommandCache _relationalCommandCache;
        private readonly RelationalQueryContext _queryContext;
        private readonly IRelationalCommand _relationalCommand;
        private CancellationToken? _externalCancellationToken;

        public RelationalBulkQueryExecutor(
            RelationalQueryContext relationalQueryContext,
            RelationalCommandCache relationalCommandCache)
        {
            _relationalCommandCache = relationalCommandCache;
            _queryContext = relationalQueryContext;
        }

        public RelationalBulkQueryExecutor(
            RelationalQueryContext relationalQueryContext,
            IRelationalCommand relationalCommand)
        {
            _relationalCommand = relationalCommand;
            _queryContext = relationalQueryContext;
        }

        private IRelationalCommand RelationalCommand
            => _relationalCommand
                ?? _relationalCommandCache?.GetRelationalCommand(_queryContext.ParameterValues)
                ?? throw new InvalidOperationException("Unknown relational command or cache.");

        public int Execute()
        {
            using (_queryContext.ConcurrencyDetector.EnterCriticalSection())
            {
#if EFCORE50
                EntityFrameworkEventSource.Log.QueryExecuting();
#endif

                return RelationalCommand.ExecuteNonQuery(
                    new RelationalCommandParameterObject(
                        _queryContext.Connection,
                        _queryContext.ParameterValues,
                        null,
                        _queryContext.Context,
                        _queryContext.CommandLogger));
            }
        }

        public IBulkQueryExecutor WithCancellationToken(CancellationToken cancellationToken)
        {
            _externalCancellationToken = cancellationToken;
            return this;
        }

        public async Task<int> ExecuteAsync()
        {
            CancellationToken cancellationToken;

            if (_externalCancellationToken.HasValue
                && _externalCancellationToken.Value != default
                && _queryContext.CancellationToken != default)
            {
                // This is rare.
                // Will be removed after whole refactor is done.
                cancellationToken = CancellationTokenSource
                    .CreateLinkedTokenSource(
                        _externalCancellationToken.Value,
                        _queryContext.CancellationToken)
                    .Token;
            }
            else
            {
                cancellationToken =
                    _externalCancellationToken
                        ?? _queryContext.CancellationToken;
            }

            using (_queryContext.ConcurrencyDetector.EnterCriticalSection())
            {
#if EFCORE50
                EntityFrameworkEventSource.Log.QueryExecuting();
#endif

                return await RelationalCommand.ExecuteNonQueryAsync(
                    new RelationalCommandParameterObject(
                        _queryContext.Connection,
                        _queryContext.ParameterValues,
                        null,
                        _queryContext.Context,
                        _queryContext.CommandLogger),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
