using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class RelationalBulkQueryExecutor : IBulkQueryExecutor
    {
        private readonly RelationalCommandCache _relationalCommandCache;
        private readonly RelationalQueryContext _queryContext;

        public RelationalBulkQueryExecutor(
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
#if EFCORE50 || EFCORE60
                EntityFrameworkEventSource.Log.QueryExecuting();
#endif

                return _relationalCommandCache
                    .RentAndPopulateRelationalCommand(_queryContext)
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
#if EFCORE50 || EFCORE60
                EntityFrameworkEventSource.Log.QueryExecuting();
#endif

                return await _relationalCommandCache
                    .RentAndPopulateRelationalCommand(_queryContext)
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
