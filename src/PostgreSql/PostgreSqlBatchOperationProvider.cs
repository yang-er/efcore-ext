using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class PostgreSqlBatchOperationProvider : RelationalBatchOperationProvider
    {
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

            var exp2 = new InsertExpression
            {
                EntityType = exp.TargetEntityType,
                SourceTable = exp.SourceTable,
                ColumnChanges = exp.ColumnChanges,
                Columns = exp.NotMatchedByTarget,
                TableChanges = exp.TableChanges,
                OnConflictUpdate = exp.Matched,
                TargetTable = exp.TargetTable,
            };

            var (command, parameters) = execution.Generate("UPSERT", null,
                _ => ((EnhancedQuerySqlGenerator)_).GetCommand(exp2));
            return (command.CommandText, parameters);
        }
    }
}