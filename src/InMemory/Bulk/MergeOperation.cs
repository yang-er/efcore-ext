using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class MergeOperation<TTarget, TSource> : BulkOperationBase<MergeRemapper<TTarget, TSource>>
    {
        private readonly Func<QueryContext, TSource, TTarget> _insertShaper;
        private readonly Func<QueryContext, TTarget, TSource, TTarget> _updateExtractor;
        private readonly bool _deleteDo;

        public MergeOperation(
            QueryContext queryContext,
            object queryExecutor,
            Func<QueryContext, TSource, TTarget> insertShaper,
            Func<QueryContext, TTarget, TSource, TTarget> updateExtractor,
            bool deleteDo)
            : base(queryContext, queryExecutor)
        {
            _insertShaper = insertShaper;
            _updateExtractor = updateExtractor;
            _deleteDo = deleteDo;
        }

        protected override void Process(MergeRemapper<TTarget, TSource> entity)
        {
            if (entity.Outer == null && entity.Inner == null)
            {
                throw new InvalidProgramException("There's something wrong with System.Linq.");
            }
            else if (entity.Outer != null && entity.Inner != null && _updateExtractor != null)
            {
                _dbContext.Update(_updateExtractor(_queryContext, entity.Outer, entity.Inner));
            }
            else if (entity.Outer != null && entity.Inner == null && _deleteDo)
            {
                _dbContext.Remove(entity.Outer);
            }
            else if (entity.Outer == null && entity.Inner != null && _insertShaper != null)
            {
                _dbContext.Add(_insertShaper(_queryContext, entity.Inner));
            }
        }
    }
}
