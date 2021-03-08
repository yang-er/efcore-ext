#nullable enable
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
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
        #region Collapsed Definitions

        /// <summary>
        /// Expanded type for query generation.
        /// </summary>
        internal static int BatchUpdate<TIdentifier, TEntity>(
            this IQueryable<TIdentifier> source,
            Expression<Func<TIdentifier, TEntity>> selector)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static int BatchUpdateJoin<TOuter, TInner, TKey>(
            this IQueryable<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TOuter>> updateSelector,
            Expression<Func<TOuter, TInner, bool>> condition)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static int BatchInsertInto<TSource>(
            this IQueryable<TSource> query)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static int Upsert<TTarget, TSource>(
            this IQueryable<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static int Upsert<TTarget>(
            this IQueryable<TTarget> set,
            Expression<Func<TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget>> updateExpression)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static int Merge<TTarget, TSource, TJoinKey>(
            this IQueryable<TTarget> targetTable,
            IQueryable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>>? insertExpression,
            [NotParameterized] bool delete)
            => throw new InvalidOperationException();

        /// <summary>
        /// Expression type for query generation.
        /// </summary>
        internal static IQueryable<TTarget> CreateSingleTuple<TSource, TTarget>(
            this IQueryable<TSource> source,
            Expression<Func<TTarget>> targets)
            => throw new InvalidOperationException();

        #endregion

        /// <summary>
        /// Convert the source local table to a fake subquery or real-query itself.
        /// </summary>
        private static IQueryable<TSource> CreateSourceTable<TTarget, TSource>(
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceEnumerable)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.NotNull(sourceEnumerable, nameof(sourceEnumerable));

            // normal subquery or table
            if (sourceEnumerable is IQueryable<TSource> sourceQuery)
            {
                return sourceQuery;
            }

            if (!typeof(TSource).IsAnonymousType())
            {
                throw new InvalidOperationException(
                    $"The source entity for upsert/merge must be anonymous objects.");
            }

            return CreateCommonTable(targetTable, sourceEnumerable.ToList());
        }

        /// <summary>
        /// Creates a temporary values table from local variables.
        /// </summary>
        internal static IQueryable<TTarget> CreateCommonTable<TSource, TTarget>(
            this IQueryable<TSource> source,
            IReadOnlyList<TTarget> targets)
        {
            Check.NotNull(source, nameof(source));
            Check.NotNull(targets, nameof(targets));

            return source.Provider.CreateQuery<TTarget>(
                Expression.Call(
                    BatchOperationMethods.CreateCommonTable.MakeGenericMethod(typeof(TSource), typeof(TTarget)),
                    source.Expression,
                    Expression.Constant(targets, typeof(IReadOnlyList<TTarget>))));
        }


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
            [NotParameterized] bool delete = false)
            where TTarget : class
            where TSource : class
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.NotNull(sourceTable, nameof(sourceTable));
            Check.NotNull(targetKey, nameof(targetKey));
            Check.NotNull(sourceKey, nameof(sourceKey));

            var targetQueryable = targetTable.IgnoreQueryFilters();
            var sourceQueryable = CreateSourceTable(targetTable, sourceTable);
            updateExpression ??= ((_, _) => null!);
            insertExpression ??= (_ => null!);

            return targetQueryable.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.MergeCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource), typeof(TJoinKey)),
                    targetQueryable.Expression,
                    sourceQueryable.Expression,
                    Expression.Quote(targetKey),
                    Expression.Quote(sourceKey),
                    Expression.Quote(updateExpression),
                    Expression.Quote(insertExpression),
                    Expression.Constant(delete)));
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

            var targetQueryable = targetTable.IgnoreQueryFilters();
            var sourceQueryable = CreateSourceTable(targetTable, sourceTable);
            updateExpression ??= ((_, _) => null!);
            insertExpression ??= (_ => null!);

            return ((IAsyncQueryProvider)targetQueryable.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.MergeCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource), typeof(TJoinKey)),
                    targetQueryable.Expression,
                    sourceQueryable.Expression,
                    Expression.Quote(targetKey),
                    Expression.Quote(sourceKey),
                    Expression.Quote(updateExpression),
                    Expression.Quote(insertExpression),
                    Expression.Constant(delete)),
                cancellationToken);
        }

        /// <summary>
        /// Perform batch delete as <c>DELETE FROM</c> operations.
        /// </summary>
        /// <typeparam name="TSource">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchDelete<TSource>(
            this IQueryable<TSource> query)
            where TSource : class
        {
            Check.NotNull(query, nameof(query));

            return query.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.BatchDelete.MakeGenericMethod(typeof(TSource)),
                    query.Expression));
        }

        /// <summary>
        /// Perform batch delete as <c>DELETE FROM</c> async operations.
        /// </summary>
        /// <typeparam name="TSource">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchDeleteAsync<TSource>(
            this IQueryable<TSource> query,
            CancellationToken cancellationToken = default)
            where TSource : class
        {
            Check.NotNull(query, nameof(query));

            return ((IAsyncQueryProvider)query.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.BatchDelete.MakeGenericMethod(typeof(TSource)),
                    query.Expression),
                cancellationToken);
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET</c> operations.
        /// </summary>
        /// <typeparam name="TSource">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="updateExpression">The update expression.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchUpdate<TSource>(
            this IQueryable<TSource> query,
            Expression<Func<TSource, TSource>> updateExpression)
            where TSource : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(updateExpression, nameof(updateExpression));

            return query.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdate.MakeGenericMethod(typeof(TSource)),
                    query.Expression,
                    Expression.Quote(updateExpression)));
        }

        /// <summary>
        /// Perform batch update as <c>UPDATE SET</c> async operations.
        /// </summary>
        /// <typeparam name="TSource">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="updateExpression">The update expression.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchUpdateAsync<TSource>(
            this IQueryable<TSource> query,
            Expression<Func<TSource, TSource>> updateExpression,
            CancellationToken cancellationToken = default)
            where TSource : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(updateExpression, nameof(updateExpression));

            return ((IAsyncQueryProvider)query.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdate.MakeGenericMethod(typeof(TSource)),
                    query.Expression,
                    Expression.Quote(updateExpression)),
                cancellationToken);
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
            condition ??= (_, __) => true;
            var outer2 = (IQueryable<TOuter>)outer;

            return outer2.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(
                        typeof(TOuter),
                        typeof(TInner),
                        typeof(TKey)),
                    outer2.Expression,
                    inner.Expression,
                    Expression.Quote(outerKeySelector),
                    Expression.Quote(innerKeySelector),
                    Expression.Quote(updateSelector),
                    Expression.Quote(condition)));
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
            condition ??= (_, __) => true;
            var outer2 = (IQueryable<TOuter>)outer;

            return ((IAsyncQueryProvider)outer2.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(
                        typeof(TOuter),
                        typeof(TInner),
                        typeof(TKey)),
                    outer2.Expression,
                    inner.Expression,
                    Expression.Quote(outerKeySelector),
                    Expression.Quote(innerKeySelector),
                    Expression.Quote(updateSelector),
                    Expression.Quote(condition)),
                cancellationToken);
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
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            if (inner.Count == 0) return 0;
            condition ??= (_, __) => true;
            var outer2 = (IQueryable<TOuter>)outer;

            return outer2.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(
                        typeof(TOuter),
                        typeof(TInner),
                        typeof(TKey)),
                    outer2.Expression,
                    outer.CreateCommonTable(inner).Expression,
                    Expression.Quote(outerKeySelector),
                    Expression.Quote(innerKeySelector),
                    Expression.Quote(updateSelector),
                    Expression.Quote(condition)));
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
            Check.NotNull(outerKeySelector, nameof(outerKeySelector));
            Check.NotNull(innerKeySelector, nameof(innerKeySelector));
            Check.NotNull(updateSelector, nameof(updateSelector));

            if (inner.Count == 0) return Task.FromResult(0);
            condition ??= (_, __) => true;
            var outer2 = (IQueryable<TOuter>)outer;

            return ((IAsyncQueryProvider)outer2.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(
                        typeof(TOuter),
                        typeof(TInner),
                        typeof(TKey)),
                    outer2.Expression,
                    outer.CreateCommonTable(inner).Expression,
                    Expression.Quote(outerKeySelector),
                    Expression.Quote(innerKeySelector),
                    Expression.Quote(updateSelector),
                    Expression.Quote(condition)),
                cancellationToken);
        }

        /// <summary>
        /// Perform batch insert into as <c>INSERT INTO SELECT FROM</c> operations.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="to">The target table.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchInsertInto<TEntity>(
            this IQueryable<TEntity> query,
            DbSet<TEntity> to)
            where TEntity : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(to, nameof(to));

            return query.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.BatchInsertIntoCollapsed.MakeGenericMethod(typeof(TEntity)),
                    query.Expression));
        }

        /// <summary>
        /// Perform batch insert into as <c>INSERT INTO SELECT FROM</c> async operations.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="to">The target table.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> BatchInsertIntoAsync<TEntity>(
            this IQueryable<TEntity> query,
            DbSet<TEntity> to,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            Check.NotNull(query, nameof(query));
            Check.NotNull(to, nameof(to));

            return ((IAsyncQueryProvider)query.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.BatchInsertIntoCollapsed.MakeGenericMethod(typeof(TEntity)),
                    query.Expression),
                cancellationToken);
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
            var inner = CreateSourceTable(set, sources);
            var outer = (IQueryable<TTarget>)set;

            return outer.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.UpsertCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource)),
                    outer.Expression,
                    inner.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? ((_, __) => null!))));
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
            var inner = CreateSourceTable(set, sources);
            var outer = (IQueryable<TTarget>)set;

            return ((IAsyncQueryProvider)outer.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.UpsertCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource)),
                    outer.Expression,
                    inner.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? ((_, __) => null!))),
                cancellationToken);
        }

        /// <summary>
        /// Perform one insert or update operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="source">The source for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity. The first parameter is the existing entity, while the second parameter is the excluded entity.</param>
        /// <returns>The affected rows.</returns>
        /// <remarks>It is suggested to replace this with <see cref="Upsert{TTarget}(DbSet{TTarget}, Expression{Func{TTarget}}, Expression{Func{TTarget, TTarget}}?)"/>.</remarks>
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
            var inner = CreateSourceTable(set, new[] { source });
            var outer = (IQueryable<TTarget>)set;

            return outer.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.UpsertCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource)),
                    outer.Expression,
                    inner.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? ((_, __) => null!))));
        }

        /// <summary>
        /// Perform one insert or update async operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <typeparam name="TSource">The data source type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="source">The source for the upserting.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity. The first parameter is the existing entity, while the second parameter is the excluded entity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        /// <remarks>It is suggested to replace this with <see cref="UpsertAsync{TTarget}(DbSet{TTarget}, Expression{Func{TTarget}}, Expression{Func{TTarget, TTarget}}?, CancellationToken)"/>.</remarks>
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
            var inner = CreateSourceTable(set, new[] { source });
            var outer = (IQueryable<TTarget>)set;

            return ((IAsyncQueryProvider)outer.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.UpsertCollapsed.MakeGenericMethod(typeof(TTarget), typeof(TSource)),
                    outer.Expression,
                    inner.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? ((_, __) => null!))),
                cancellationToken);
        }

        /// <summary>
        /// Perform one insert or update operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity.</param>
        /// <returns>The affected rows.</returns>
        public static int Upsert<TTarget>(
            this DbSet<TTarget> set,
            Expression<Func<TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget>>? updateExpression = null)
            where TTarget : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(insertExpression, nameof(insertExpression));
            var target = (IQueryable<TTarget>)set;

            return target.Provider.Execute<int>(
                Expression.Call(
                    BatchOperationMethods.UpsertOneCollapsed.MakeGenericMethod(typeof(TTarget)),
                    target.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? (_ => null!))));
        }

        /// <summary>
        /// Perform one insert or update async operation.
        /// </summary>
        /// <typeparam name="TTarget">The entity type.</typeparam>
        /// <param name="set">The entity set.</param>
        /// <param name="insertExpression">The expression for inserting new entity.</param>
        /// <param name="updateExpression">The expression for updating the existing entity.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task for affected rows.</returns>
        public static Task<int> UpsertAsync<TTarget>(
            this DbSet<TTarget> set,
            Expression<Func<TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget>>? updateExpression = null,
            CancellationToken cancellationToken = default)
            where TTarget : class
        {
            Check.NotNull(set, nameof(set));
            Check.NotNull(insertExpression, nameof(insertExpression));
            var target = (IQueryable<TTarget>)set;

            return ((IAsyncQueryProvider)target.Provider).ExecuteAsync<Task<int>>(
                Expression.Call(
                    BatchOperationMethods.UpsertOneCollapsed.MakeGenericMethod(typeof(TTarget)),
                    target.Expression,
                    Expression.Quote(insertExpression),
                    Expression.Quote(updateExpression ?? (_ => null!))),
                cancellationToken);
        }
    }
}
