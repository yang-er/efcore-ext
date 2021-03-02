using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class UpsertOperation<TTarget, TSource> : BulkOperationBase<MergeRemapper<TTarget, TSource>>
    {
        private readonly Func<QueryContext, TSource, TTarget> _insertShaper;
        private readonly Func<QueryContext, TTarget, TTarget, TTarget> _updateExtractor;

        public UpsertOperation(
            QueryContext queryContext,
            object queryExecutor,
            Func<QueryContext, TSource, TTarget> insertShaper,
            Func<QueryContext, TTarget, TTarget, TTarget> updateExtractor)
            : base(queryContext, queryExecutor)
        {
            _insertShaper = insertShaper;
            _updateExtractor = updateExtractor;
        }

        protected override void Process(MergeRemapper<TTarget, TSource> entity)
        {
            var excluded = _insertShaper(_queryContext, entity.Inner);

            if (entity.Outer == null)
            {
                _dbContext.Add(excluded);
            }
            else if (_updateExtractor != null)
            {
                _dbContext.Update(_updateExtractor(_queryContext, entity.Outer, excluded));
            }
        }
    }
}
