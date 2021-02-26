using Microsoft.EntityFrameworkCore.Query;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class SelectIntoOperation<TEntity> : BulkOperationBase<TEntity>
    {
        public SelectIntoOperation(QueryContext queryContext, object queryExecutor)
            : base(queryContext, queryExecutor)
        {
        }

        protected override void Process(TEntity entity)
        {
            _dbContext.Add(entity);
        }
    }
}
