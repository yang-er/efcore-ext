using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public abstract class BulkOperationBase<TEntity> : IBulkQueryExecutor
    {
        protected readonly object _queryEnumerable;
        protected readonly DbContext _dbContext;
        protected readonly QueryContext _queryContext;

        protected BulkOperationBase(QueryContext queryContext, object queryExecutor)
        {
            _queryEnumerable = queryExecutor;
            _queryContext = queryContext;
            _dbContext = queryContext.Context;
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

            return await _dbContext.SaveChangesAsync(_queryContext.CancellationToken);
        }
    }
}
