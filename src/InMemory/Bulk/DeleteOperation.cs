using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class DeleteOperation<TEntity, TResult> : IBulkQueryExecutor
    {
        private readonly TResult _queryEnumerable;
        private readonly DbContext _dbContext;
        private CancellationToken _cancellationToken;

        public DeleteOperation(
            QueryContext queryContext,
            TResult queryExecutor)
        {
            _queryEnumerable = queryExecutor;
            _dbContext = queryContext.Context;
            _cancellationToken = queryContext.CancellationToken;
        }

        public int Execute()
        {
            foreach (var entry in (IEnumerable<TEntity>)_queryEnumerable)
            {
                _dbContext.Remove(entry);
            }

            return _dbContext.SaveChanges();
        }

        public async Task<int> ExecuteAsync()
        {
            await foreach (var entry in (IAsyncEnumerable<TEntity>)_queryEnumerable)
            {
                _dbContext.Remove(entry);
            }

            return await _dbContext.SaveChangesAsync(_cancellationToken);
        }

        public IBulkQueryExecutor WithCancellationToken(CancellationToken cancellationToken)
        {
            if (_cancellationToken == default)
            {
                _cancellationToken = cancellationToken;
            }
            else if (cancellationToken != default)
            {
                _cancellationToken = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken, _cancellationToken)
                    .Token;
            }

            return this;
        }
    }
}
