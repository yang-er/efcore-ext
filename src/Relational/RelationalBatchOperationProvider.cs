using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class RelationalBatchOperationProvider : IBatchOperationProvider
    {
        #region IQueryable<>.BatchDelete()

        public int BatchDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            var executor = GetSqlDelete(context, query);
            return executor.Execute();
        }

        public Task<int> BatchDeleteAsync<T>(
            DbContext context,
            IQueryable<T> query,
            CancellationToken cancellationToken)
            where T : class
        {
            var executor = GetSqlDelete(context, query);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            QueryRewriter.ParseDelete(
                context, query,
                out var deleteExpression, out var queryRewritingContext);

            return queryRewritingContext.Generate(deleteExpression);
        }

        #endregion

        #region IQueryable<>.BatchUpdate(old => new Target)

        public int BatchUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression)
            where T : class
        {
            var executor = GetSqlUpdate(context, query, updateExpression);
            return executor.Execute();
        }

        public Task<int> BatchUpdateAsync<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken)
            where T : class
        {
            var executor = GetSqlUpdate(context, query, updateExpression);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateSelector)
            where T : class
        {
            QueryRewriter.ParseUpdate(
                context, query, updateSelector,
                out var updateExpression, out var queryRewritingContext);

            return queryRewritingContext.Generate(updateExpression);
        }

        #endregion

        #region IQueryable<>.BatchInsertInto(DbSet<>)

        public int BatchInsertInto<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            var executor = GetSqlSelectInto(context, query);
            return executor.Execute();
        }

        public Task<int> BatchInsertIntoAsync<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken)
            where T : class
        {
            var executor = GetSqlSelectInto(context, query);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlSelectInto<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            QueryRewriter.ParseSelectInto(
                context, query,
                out var updateExpression, out var queryRewritingContext);

            return queryRewritingContext.Generate(updateExpression);
        }

        #endregion

        #region DbSet<>.BatchUpdateJoin(innerList, okey, ikey, upd, cond)

        public int BatchUpdateJoin<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IReadOnlyList<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null)
            where TOuter : class
            where TInner : class
        {
            var executor = GetSqlUpdateJoinList(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return executor.Execute();
        }

        public Task<int> BatchUpdateJoinAsync<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IReadOnlyList<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null,
            CancellationToken cancellationToken = default)
            where TOuter : class
            where TInner : class
        {
            var executor = GetSqlUpdateJoinList(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlUpdateJoinList<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IReadOnlyList<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null)
            where TOuter : class
            where TInner : class
        {
            QueryRewriter.ParseUpdateJoinList(
                context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition,
                out var updateExpression, out var queryRewritingContext);

            if (updateExpression == null) return new NullNonQueryExecutor();
            return queryRewritingContext.Generate(updateExpression);
        }

        #endregion

        #region DbSet<>.BatchUpdateJoin(innerQueryable, okey, ikey, upd, cond)

        public int BatchUpdateJoin<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null)
            where TOuter : class
            where TInner : class
        {
            var executor = GetSqlUpdateJoinQueryable(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return executor.Execute();
        }

        public Task<int> BatchUpdateJoinAsync<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null,
            CancellationToken cancellationToken = default)
            where TOuter : class
            where TInner : class
        {
            var executor = GetSqlUpdateJoinQueryable(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlUpdateJoinQueryable<TOuter, TInner, TKey>(
            DbContext context,
            DbSet<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null)
            where TOuter : class
            where TInner : class
        {
            QueryRewriter.ParseUpdateJoinQueryable(
                context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition,
                out var updateExpression, out var queryRewritingContext);

            return queryRewritingContext.Generate(updateExpression);
        }

        #endregion

        #region DbSet<>.Merge(source, tkey, skey, upd, ins, del)

        public int Merge<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            var executor = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return executor.Execute();
        }

        public Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            var executor = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlMerge<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable2,
            Type joinKeyType,
            LambdaExpression targetKey,
            LambdaExpression sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            throw new NotSupportedException("Default batch operation provider doesn't support MERGE INTO.");
        }

        #endregion

        #region DbSet<>.Upsert(sources, src => new Target, (existing, excluded) => new Target)

        public int Upsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            var executor = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return executor.Execute();
        }

        public Task<int> UpsertAsync<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            var executor = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return executor.WithCancellationToken(cancellationToken).ExecuteAsync();
        }

        protected virtual INonQueryExecutor GetSqlUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            throw new NotSupportedException("Default batch operation provider doesn't support UPSERT.");
        }

        #endregion
    }
}
