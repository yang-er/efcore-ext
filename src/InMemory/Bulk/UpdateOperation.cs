using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class UpdateOperation<TEntity> : BulkOperationBase<UpdateRemapper<TEntity>>
    {
        private readonly Func<QueryContext, UpdateRemapper<TEntity>, TEntity> _updaterAndExtractor;

        public UpdateOperation(
            QueryContext queryContext,
            object queryExecutor,
            Func<QueryContext, UpdateRemapper<TEntity>, TEntity> updaterAndExtractor)
            : base(queryContext, queryExecutor)
        {
            _updaterAndExtractor = updaterAndExtractor;
        }

        protected override void Process(UpdateRemapper<TEntity> source)
        {
            _dbContext.Update(_updaterAndExtractor(_queryContext, source));
        }
    }
}
