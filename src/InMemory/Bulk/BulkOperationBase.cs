using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public abstract class BulkOperationBase<TEntity> : IBulkQueryExecutor
    {
        protected readonly object _queryEnumerable;
        protected readonly DbContext _dbContext;
        protected readonly QueryContext _queryContext;
        private CancellationToken _cancellationToken;

        protected BulkOperationBase(QueryContext queryContext, object queryExecutor)
        {
            _queryEnumerable = queryExecutor;
            _queryContext = queryContext;
            _dbContext = queryContext.Context;
            _cancellationToken = queryContext.CancellationToken;
        }

        protected abstract void Process(TEntity entity);

        public virtual int Execute()
        {
            foreach (var entry in (IEnumerable<TEntity>)_queryEnumerable)
            {
                Process(entry);
            }

            return _dbContext.SaveChanges();
        }

        public virtual async Task<int> ExecuteAsync()
        {
            await foreach (var entry in (IAsyncEnumerable<TEntity>)_queryEnumerable)
            {
                Process(entry);
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
