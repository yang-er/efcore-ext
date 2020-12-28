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
        public (string, IEnumerable<object>) ToParametrizedSql<TEntity>(
            DbContext context,
            IQueryable<TEntity> query)
            where TEntity : class
        {
            var queryRewritingContext = TranslationStrategy.System(query);
            var selectExpression = queryRewritingContext.SelectExpression;
            var (command, parameters) = queryRewritingContext.Generate(selectExpression);
            return (command.CommandText, parameters);
        }

        #region IQueryable<>.BatchDelete()

        public int BatchDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlDelete(context, query);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public Task<int> BatchDeleteAsync<T>(
            DbContext context,
            IQueryable<T> query,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlDelete(context, query);
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            QueryRewriter.ParseDelete(
                context, query,
                out var deleteExpression, out var queryRewritingContext);

            var (command, parameters) = queryRewritingContext.Generate(deleteExpression);
            return (command.CommandText, parameters);
        }

        #endregion

        #region IQueryable<>.BatchUpdate(old => new Target)

        public int BatchUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlUpdate(context, query, updateExpression);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public Task<int> BatchUpdateAsync<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlUpdate(context, query, updateExpression);
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateSelector)
            where T : class
        {
            QueryRewriter.ParseUpdate(
                context, query, updateSelector,
                out var updateExpression, out var queryRewritingContext);

            var (command, parameters) = queryRewritingContext.Generate(updateExpression);
            return (command.CommandText, parameters);
        }

        #endregion

        #region IQueryable<>.BatchInsertInto(DbSet<>)

        public int BatchInsertInto<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            var (sql, parameters) = GetSqlSelectInto(context, query);
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public Task<int> BatchInsertIntoAsync<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, parameters) = GetSqlSelectInto(context, query);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlSelectInto<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            QueryRewriter.ParseSelectInto(
                context, query,
                out var updateExpression, out var queryRewritingContext);

            var (command, parameters) = queryRewritingContext.Generate(updateExpression);
            return (command.CommandText, parameters);
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
            var (sql, sqlParameters) = GetSqlUpdateJoinList(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
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
            var (sql, sqlParameters) = GetSqlUpdateJoinList(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlUpdateJoinList<TOuter, TInner, TKey>(
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
            throw new NotSupportedException("Default batch operation provider doesn't support UPDATE JOIN.");
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
            var (sql, sqlParameters) = GetSqlUpdateJoinQueryable(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
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
            var (sql, sqlParameters) = GetSqlUpdateJoinQueryable(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlUpdateJoinQueryable<TOuter, TInner, TKey>(
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

            var (command, parameters) = queryRewritingContext.Generate(updateExpression);
            return (command.CommandText, parameters);
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
            var (sql, parameters) = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return context.Database.ExecuteSqlRaw(sql, parameters);
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
            var (sql, parameters) = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlMerge<TTarget, TSource>(
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
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRaw(sql, parameters);
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
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        protected virtual (string, IEnumerable<object>) GetSqlUpsert<TTarget, TSource>(
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
