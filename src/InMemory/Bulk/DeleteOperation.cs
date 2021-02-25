using Microsoft.EntityFrameworkCore.Query;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class DeleteOperation<TEntity> : BulkOperationBase<TEntity>
    {
        public DeleteOperation(QueryContext queryContext, object queryExecutor)
            : base(queryContext, queryExecutor)
        {
        }

        protected override void Process(TEntity entity)
        {
            _dbContext.Remove(entity);
        }
    }
}
