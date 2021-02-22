#nullable enable
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provide static extensions for batch operations.
    /// </summary>
    public static class BatchOperationExtensions
    {
        /// <summary>
        /// Perform merge as <c>MERGE INTO</c> operations.
        /// </summary>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TSource">The source table type.</typeparam>
        /// <typeparam name="TJoinKey">The join key type.</typeparam>
        /// <param name="delete">When not matched in source table, whether to delete entities.</param>
        /// <param name="insertExpression">When not matched in target table, the expression to insert new entities.</param>
        /// <param name="updateExpression">When matched in both, the expression to update existing entities.</param>
        /// <param name="sourceKey">The source key to match.</param>
        /// <param name="sourceTable">The source table to operate.</param>
        /// <param name="targetKey">The target key to match.</param>
        /// <param name="targetTable">The target table to operate.</param>
        /// <returns>The affected rows.</returns>
        public static int Merge<TTarget, TSource, TJoinKey>(
            this DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>>? updateExpression = null,
            Expression<Func<TSource, TTarget>>? insertExpression = null,
            bool delete = false)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.NotNull(sourceTable, nameof(sourceTable));
            Check.NotNull(targetKey, nameof(targetKey));
            Check.NotNull(sourceKey, nameof(sourceKey));

            var context = targetTable.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.Merge(context,
                targetTable, sourceTable,
                targetKey, sourceKey,
                updateExpression, insertExpression, delete);
        }

        /// <summary>
        /// Perform merge as <c>MERGE INTO</c> async operations.
        /// </summary>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TSource">The source table type.</typeparam>
        /// <typeparam name="TJoinKey">The join key type.</typeparam>
        /// <param name="delete">When not matched in source table, whether to delete entities.</param>
        /// <param name="insertExpression">When not matched in target table, the expression to insert new entities.</param>
        /// <param name="updateExpression">When matched in both, the expression to update existing entities.</param>
        /// <param name="sourceKey">The source key to match.</param>
        /// <param name="sourceTable">The source table to operate.</param>
        /// <param name="targetKey">The target key to match.</param>
        /// <param name="targetTable">The target table to operate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
            this DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>>? updateExpression = null,
            Expression<Func<TSource, TTarget>>? insertExpression = null,
            bool delete = false,
            CancellationToken cancellationToken = default)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.NotNull(sourceTable, nameof(sourceTable));
            Check.NotNull(targetKey, nameof(targetKey));
            Check.NotNull(sourceKey, nameof(sourceKey));

            var context = targetTable.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.MergeAsync(context,
                targetTable, sourceTable,
                targetKey, sourceKey,
                updateExpression, insertExpression, delete,
                cancellationToken);
        }

        /// <summary>
        /// Perform batch delete as <c>DELETE FROM</c> operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchDelete<T>(
            this IQueryable<T> query)
            where T : class
        {
            Check.NotNull(query, nameof(query));

            var context = query.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchDelete(context, query);
        }

        /// <summary>
        /// Perform batch delete as <c>DELETE FROM</c> async operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchDeleteAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Check.NotNull(query, nameof(query));

            var context = query.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchDeleteAsync(context, query, cancellationToken);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET</c> operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="updateExpression">The update expression.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchUpdate<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression)
            where T : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(updateExpression, nameof(updateExpression));

            var context = query.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdate(context, query, updateExpression);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET</c> async operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="updateExpression">The update expression.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchUpdateAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(updateExpression, nameof(updateExpression));

            var context = query.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateAsync(context, query, updateExpression, cancellationToken);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET INNER JOIN</c> operations.
        /// </summary>
        /// <typeparam name="TOuter">The outer entity type.</typeparam>
        /// <typeparam name="TInner">The inner entity type.</typeparam>
        /// <typeparam name="TKey">The join key type.</typeparam>
        /// <param name="outer">The outer source.</param>
        /// <param name="inner">The inner source.</param>
        /// <param name="outerKeySelector">The outer key selector.</param>
        /// <param name="innerKeySelector">The inner key selector.</param>
        /// <param name="updateSelector">The update expression.</param>
        /// <param name="condition">The condition.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchUpdateJoin<TOuter, TInner, TKey>(
            this DbSet<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>>? condition = null)
            where TOuter : class
            where TInner : class
        {
            Check.NotNull(outer, nameof(outer));
            Check.NotNull(inner, nameof(inner));
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            var context = outer.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateJoin(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET INNER JOIN</c> async operations.
        /// </summary>
        /// <typeparam name="TOuter">The outer entity type.</typeparam>
        /// <typeparam name="TInner">The inner entity type.</typeparam>
        /// <typeparam name="TKey">The join key type.</typeparam>
        /// <param name="outer">The outer source.</param>
        /// <param name="inner">The inner source.</param>
        /// <param name="outerKeySelector">The outer key selector.</param>
        /// <param name="innerKeySelector">The inner key selector.</param>
        /// <param name="updateSelector">The update expression.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchUpdateJoinAsync<TOuter, TInner, TKey>(
            this DbSet<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>>? condition = null,
            CancellationToken cancellationToken = default)
            where TOuter : class
            where TInner : class
        {
            Check.NotNull(outer, nameof(outer));
            Check.NotNull(inner, nameof(inner));
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            var context = outer.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateJoinAsync(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition, cancellationToken);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET INNER JOIN</c> operations.
        /// </summary>
        /// <typeparam name="TOuter">The outer entity type.</typeparam>
        /// <typeparam name="TInner">The inner entity type.</typeparam>
        /// <typeparam name="TKey">The join key type.</typeparam>
        /// <param name="outer">The outer source.</param>
        /// <param name="inner">The inner source.</param>
        /// <param name="outerKeySelector">The outer key selector.</param>
        /// <param name="innerKeySelector">The inner key selector.</param>
        /// <param name="updateSelector">The update expression.</param>
        /// <param name="condition">The condition.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchUpdateJoin<TOuter, TInner, TKey>(
            this DbSet<TOuter> outer,
            IReadOnlyList<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>>? condition = null)
            where TOuter : class
            where TInner : class
        {
            Check.NotNull(outer, nameof(outer));
            Check.NotNull(inner, nameof(inner));
            if (inner.Count == 0) return 0;
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            var context = outer.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateJoin(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET INNER JOIN</c> async operations.
        /// </summary>
        /// <typeparam name="TOuter">The outer entity type.</typeparam>
        /// <typeparam name="TInner">The inner entity type.</typeparam>
        /// <typeparam name="TKey">The join key type.</typeparam>
        /// <param name="outer">The outer source.</param>
        /// <param name="inner">The inner source.</param>
        /// <param name="outerKeySelector">The outer key selector.</param>
        /// <param name="innerKeySelector">The inner key selector.</param>
        /// <param name="updateSelector">The update expression.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchUpdateJoinAsync<TOuter, TInner, TKey>(
            this DbSet<TOuter> outer,
            IReadOnlyList<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>>? condition = null,
            CancellationToken cancellationToken = default)
            where TOuter : class
            where TInner : class
        {
            Check.NotNull(outer, nameof(outer));
            Check.NotNull(inner, nameof(inner));
            if (inner.Count == 0) return Task.FromResult(0);
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            var context = outer.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateJoinAsync(context, outer, inner, outerKeySelector, innerKeySelector, updateSelector, condition, cancellationToken);
        }

        /// <summary>
        /// Perform batch insert into as <c>INSERT INTO SELECT FROM</c> operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="to">The target table.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchInsertInto<T>(
            this IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(to, nameof(to));

            var context = to.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchInsertInto(context, query, to);
        }

        /// <summary>
        /// Perform batch insert into as <c>INSERT INTO SELECT FROM</c> async operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="to">The target table.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchInsertIntoAsync<T>(
            this IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(to, nameof(to));

            var context = to.GetDbContext();
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchInsertIntoAsync(context, query, to, cancellationToken);
        }

        /// <summary>
        /// Perform one insert or update operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="sources">The sources for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity. The first parameter is the existing entity, while the second parameter is the excluded entity.</param>
        /// <returns>The affected rows.</returns>
        public static int Upsert<TTarget, TSource>(
            this DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression = null)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(sources, nameof(sources));
            Check.NotNull(insertExpression, nameof(insertExpression));

            var context = set.GetService<ICurrentDbContext>().Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.Upsert(context, set, sources, insertExpression, updateExpression);
        }

        /// <summary>
        /// Perform one insert or update async operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="sources">The sources for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity. The first parameter is the existing entity, while the second parameter is the excluded entity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> UpsertAsync<TTarget, TSource>(
            this DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression = null,
            CancellationToken cancellationToken = default)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(sources, nameof(sources));
            Check.NotNull(insertExpression, nameof(insertExpression));

            var context = set.GetService<ICurrentDbContext>().Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.UpsertAsync(context, set, sources, insertExpression, updateExpression, cancellationToken);
        }

        /// <summary>
        /// Perform one insert or update operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="source">The source for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity.</param>
        /// <returns>The affected rows.</returns>
        public static int Upsert<TTarget, TSource>(
            this DbSet<TTarget> set,
            TSource source,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression = null)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(source, nameof(source));
            Check.NotNull(insertExpression, nameof(insertExpression));

            var context = set.GetService<ICurrentDbContext>().Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.Upsert(context, set, new[] { source }, insertExpression, updateExpression);
        }

        /// <summary>
        /// Perform one insert or update async operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="source">The source for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> UpsertAsync<TTarget, TSource>(
            this DbSet<TTarget> set,
            TSource source,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>>? updateExpression = null,
            CancellationToken cancellationToken = default)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(source, nameof(source));
            Check.NotNull(insertExpression, nameof(insertExpression));

            var context = set.GetService<ICurrentDbContext>().Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.UpsertAsync(context, set, new[] { source }, insertExpression, updateExpression, cancellationToken);
        }
    }
}
