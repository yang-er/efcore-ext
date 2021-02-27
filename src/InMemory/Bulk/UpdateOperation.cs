using Microsoft.EntityFrameworkCore.Query;
using System;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class UpdateOperation<TSource, TEntity> : BulkOperationBase<TSource>
    {
        private readonly Func<QueryContext, TSource, TEntity> _updaterAndExtractor;

        public UpdateOperation(QueryContext queryContext, object queryExecutor, Func<QueryContext, TSource, TEntity> updaterAndExtractor)
            : base(queryContext, queryExecutor)
        {
            _updaterAndExtractor = updaterAndExtractor;
        }

        protected override void Process(TSource source)
        {
            _dbContext.Update(_updaterAndExtractor(_queryContext, source));
        }
    }
}
