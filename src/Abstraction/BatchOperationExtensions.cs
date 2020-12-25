#nullable enable
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    public static class BatchOperationExtensions
    {
        private static readonly Func<IQueryProvider, IQueryContextFactory> GetQueryContextFactory
            = Internals.CreateLambda<IQueryProvider, IQueryContextFactory>(param => param
                 .As<EntityQueryProvider>()
                 .AccessField("_queryCompiler")
                 .As<QueryCompiler>()
                 .AccessField("_queryContextFactory")
                 .As<IQueryContextFactory>())
            .Compile();

        private static QueryContextDependencies AccessDependencies(IQueryProvider queryProvider)
            => GetQueryContextFactory(queryProvider)
                .Private<QueryContextDependencies>("_dependencies");

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
            var context = targetTable.GetService<ICurrentDbContext>().Context;
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
            var context = targetTable.GetService<ICurrentDbContext>().Context;
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
            var context = AccessDependencies(query.Provider).StateManager.Context;
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
            var context = AccessDependencies(query.Provider).StateManager.Context;
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
            var context = AccessDependencies(query.Provider).StateManager.Context;
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
            var context = AccessDependencies(query.Provider).StateManager.Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchUpdateAsync(context, query, updateExpression, cancellationToken);
        }

        /// <summary>
        /// Perform batch insert into as <c>INSERT INTO SELECT FROM</c> operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The entity query.</param>
        /// <param name="_">The target table.</param>
        /// <returns>The affected rows.</returns>
        public static int BatchInsertInto<T>(
            this IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            var context = to.GetService<ICurrentDbContext>().Context;
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
            var context = to.GetService<ICurrentDbContext>().Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.BatchInsertIntoAsync(context, query, to, cancellationToken);
        }

        /// <summary>
        /// Translate the <see cref="IQueryable{T}"/> to parametrized SQL.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <param name="query">The query expression.</param>
        /// <returns>The query command text and parameters.</returns>
        public static (string, IEnumerable<object>) ToParametrizedSql<T>(
            this IQueryable<T> query)
            where T : class
        {
            var context = AccessDependencies(query.Provider).StateManager.Context;
            var provider = context.GetService<IBatchOperationProvider>();
            return provider.ToParametrizedSql(context, query);
        }
    }
}
