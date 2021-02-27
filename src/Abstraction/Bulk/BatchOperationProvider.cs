#nullable enable
#pragma warning disable CS1591
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class;

        int Upsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression)
            where TTarget : class
            where TSource : class;

        Task<int> UpsertAsync<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class;
    }
}
