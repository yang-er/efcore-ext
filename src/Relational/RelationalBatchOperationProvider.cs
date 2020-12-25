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
        public virtual int Merge<TTarget, TSource, TJoinKey>(
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
            throw new NotSupportedException("Default batch operation provider doesn't support MERGE INTO.");
        }

        public virtual Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
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
            throw new NotSupportedException("Default batch operation provider doesn't support MERGE INTO.");
        }

        public int BatchDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlCommand(query, context, "DELETE");
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public Task<int> BatchDeleteAsync<T>(
            DbContext context,
            IQueryable<T> query,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlCommand(query, context, "DELETE");
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        public int BatchUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlCommand(query.Select(updateExpression), context, "UPDATE");
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public Task<int> BatchUpdateAsync<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, sqlParameters) = GetSqlCommand(query.Select(updateExpression), context, "UPDATE");
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }

        public int BatchInsertInto<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            var (sql, parameters) = GetSqlCommand(query, context, "INSERT");
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public Task<int> BatchInsertIntoAsync<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken)
            where T : class
        {
            var (sql, parameters) = GetSqlCommand(query, context, "INSERT");
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        public (string, IEnumerable<object>) ToParametrizedSql<TEntity>(
            DbContext context,
            IQueryable<TEntity> query)
            where TEntity : class
        {
            return GetSqlCommand(query, context, "SELECT");
        }

        public virtual (string, IEnumerable<object>) GetSqlCommand<T>(
            IQueryable<T> query,
            DbContext context,
            string type)
            where T : class
        {
            var entityType = context.Model.FindEntityType(typeof(T));
            var execution = TranslationStrategy.Go(context, query);
            var (command, parameters) = execution.Generate(type, entityType);
            return (command.CommandText, parameters);
        }

        private IQueryable<TOuter> CreateUpdateJoinQuery<TOuter, TInner, TKey>(
            IQueryable<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition = null)
        {
            return outer
                .Join(inner, outerKeySelector, innerKeySelector, (outer, inner) => new { outer, inner })
                .WhereIf(condition.Combine(new { outer = default(TOuter), inner = default(TInner) }, a => a.outer, b => b.inner))
                .Select(updateSelector.Combine(new { outer = default(TOuter), inner = default(TInner) }, a => a.outer, b => b.inner));
        }

        public virtual int BatchUpdateJoin<TOuter, TInner, TKey>(
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
            var (sql, sqlParameters) = GetSqlCommand(CreateUpdateJoinQuery(outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition), context, "UPDATE");
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public virtual Task<int> BatchUpdateJoinAsync<TOuter, TInner, TKey>(
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
            var (sql, sqlParameters) = GetSqlCommand(CreateUpdateJoinQuery(outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition), context, "UPDATE");
            return context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);
        }
    }
}
