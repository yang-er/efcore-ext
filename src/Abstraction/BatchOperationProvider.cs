#nullable enable
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    /// <summary>
    /// The batch operation provider.
    /// </summary>
    /// <remarks>
    /// The service lifetime should be <see cref="ServiceLifetime.Singleton"/>.
    /// </remarks>
    public interface IBatchOperationProvider
    {
        int Merge<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>>? updateExpression,
            Expression<Func<TSource, TTarget>>? insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class;

        Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>>? updateExpression,
            Expression<Func<TSource, TTarget>>? insertExpression,
            bool delete,
            CancellationToken cancellationToken = default)
            where TTarget : class
            where TSource : class;

        int BatchDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class;

        Task<int> BatchDeleteAsync<T>(
            DbContext context,
            IQueryable<T> query,
            CancellationToken cancellationToken = default)
            where T : class;

        int BatchUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression)
            where T : class;

        Task<int> BatchUpdateAsync<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default)
            where T : class;

        int BatchInsertInto<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to)
            where T : class;

        Task<int> BatchInsertIntoAsync<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken = default)
            where T : class;

        (string, IEnumerable<object>) ToParametrizedSql<TEntity>(
            DbContext context,
            IQueryable<TEntity> query)
            where TEntity : class;
    }
}
