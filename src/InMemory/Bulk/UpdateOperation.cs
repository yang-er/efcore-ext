using Microsoft.EntityFrameworkCore.Query;
using System;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class UpdateOperation<TEntity> : BulkOperationBase<TEntity>
    {
        private readonly Action<QueryContext, TEntity> _updater;

        public UpdateOperation(QueryContext queryContext, object queryExecutor, Action<QueryContext, TEntity> updater)
            : base(queryContext, queryExecutor)
        {
            _updater = updater;
        }

        protected override void Process(TEntity entity)
        {
            _updater(_queryContext, entity);
            _dbContext.Update(entity);
        }
    }
}
