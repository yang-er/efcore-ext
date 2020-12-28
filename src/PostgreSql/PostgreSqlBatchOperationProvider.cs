using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class PostgreSqlBatchOperationProvider : RelationalBatchOperationProvider
    {
        protected override (string, IEnumerable<object>) GetSqlUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            QueryRewriter.ParseUpsert(
                context, targetTable, sourceTable,
                insertExpression, updateExpression,
                out var upsertExpression, out var queryRewritingContext);

            if (upsertExpression == null)
                return ("SELECT 0", Array.Empty<object>());

            var (command, parameters) = queryRewritingContext.Generate(upsertExpression);
            return (command.CommandText, parameters);
        }
    }
}