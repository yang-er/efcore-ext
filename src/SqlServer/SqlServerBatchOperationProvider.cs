using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class SqlServerBatchOperationProvider : RelationalBatchOperationProvider
    {
        public override int Merge<TTarget, TSource, TJoinKey>(
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

        public override Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
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

        public override int Upsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public override Task<int> UpsertAsync<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        private static (string, IEnumerable<object>) GetSqlUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            MergeQueryRewriter.ParseUpsert(
                context, targetTable, sourceTable,
                insertExpression, updateExpression,
                out var exp, out var execution);

            if (exp == null)
                return ("SELECT 0", Array.Empty<object>());

            var (command, parameters) = execution.Generate("MERGE", null,
                _ => ((EnhancedQuerySqlGenerator)_).GetCommand(exp));
            return (command.CommandText, parameters);
        }

        private static (string, IEnumerable<object>) GetSqlMerge<TTarget, TSource>(
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
            MergeQueryRewriter.ParseMerge(
                context, targetTable, sourceTable2,
                joinKeyType, targetKey, sourceKey,
                updateExpression, insertExpression, delete,
                out var exp, out var execution);

            if (exp == null)
                return (delete
                    ? $"TRUNCATE TABLE [{context.Model.FindEntityType(typeof(TTarget)).GetTableName()}]"
                    : $"SELECT 0", Array.Empty<object>());

            var (command, parameters) = execution.Generate("MERGE", null,
                _ => ((EnhancedQuerySqlGenerator)_).GetCommand(exp));
            return (command.CommandText, parameters);
        }
    }
}
