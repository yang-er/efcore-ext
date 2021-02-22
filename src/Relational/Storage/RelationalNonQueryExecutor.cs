using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query.Internal
{
    public class RelationalNonQueryExecutor
    {
        private readonly RelationalCommandCache _relationalCommandCache;
        private readonly RelationalQueryContext _queryContext;

        public RelationalNonQueryExecutor(
            RelationalQueryContext relationalQueryContext,
            RelationalCommandCache relationalCommandCache)
        {
            _relationalCommandCache = relationalCommandCache;
            _queryContext = relationalQueryContext;
        }

        public int Execute()
        {
            using (_queryContext.ConcurrencyDetector.EnterCriticalSection())
            {
                EntityFrameworkEventSource.Log.QueryExecuting();

                return _relationalCommandCache
                    .GetRelationalCommand(_queryContext.ParameterValues)
                    .ExecuteNonQuery(
                        new RelationalCommandParameterObject(
                            _queryContext.Connection,
                            _queryContext.ParameterValues,
                            null,
                            _queryContext.Context,
                            _queryContext.CommandLogger));
            }
        }

        public async Task<int> ExecuteAsync()
        {
            using (_queryContext.ConcurrencyDetector.EnterCriticalSection())
            {
                EntityFrameworkEventSource.Log.QueryExecuting();

                return await _relationalCommandCache
                    .GetRelationalCommand(_queryContext.ParameterValues)
                    .ExecuteNonQueryAsync(
                        new RelationalCommandParameterObject(
                            _queryContext.Connection,
                            _queryContext.ParameterValues,
                            null,
                            _queryContext.Context,
                            _queryContext.CommandLogger),
                        _queryContext.CancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
