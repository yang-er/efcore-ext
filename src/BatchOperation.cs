using Microsoft.EntityFrameworkCore.Bulk;
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
        public static int BatchDelete<T>(this IQueryable<T> query) where T : class
        {
            DbContext context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlCommand(query, context, "DELETE");
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public static async Task<int> BatchDeleteAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default) where T : class
        {
            DbContext context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlCommand(query, context, "DELETE");
            return await context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken).ConfigureAwait(false);
        }

        public static int BatchUpdate<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression) where T : class
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlCommand(query.Select(updateExpression), context, "UPDATE");
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }

        public static async Task<int> BatchUpdateAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default) where T : class
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlCommand(query.Select(updateExpression), context, "UPDATE");
            return await context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken).ConfigureAwait(false);
        }

        public static int BatchInsertInto<T>(
            this IQueryable<T> query, DbSet<T> _) where T : class
        {
            var context = query.GetDbContext();
            var (sql, parameters) = GetSqlCommand(query, context, "INSERT");
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public static async Task<int> BatchInsertIntoAsync<T>(
            this IQueryable<T> query,
            DbSet<T> _,
            CancellationToken cancellationToken = default) where T : class
        {
            var context = query.GetDbContext();
            var (sql, parameters) = GetSqlCommand(query, context, "INSERT");
            return await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        public static (string, IEnumerable<object>) GetSqlCommand<T>(
            IQueryable<T> query, DbContext context, string type) where T : class
        {
            var entityType = context.Model.FindEntityType(typeof(T));
            var execution = TranslationStrategy.Go(query);
            var command = execution.GetCommand(type, entityType);
            var parameters = execution.CreateParameter(command);
            return (command.CommandText, parameters);
        }
    }
}
