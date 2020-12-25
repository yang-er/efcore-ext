using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableBatchDeleteExtensions
    {
        public static int BatchDelete<T>(this IQueryable<T> query) where T : class
        {
            DbContext context = query.GetDbContext();
            (string sql, var sqlParameters) = GetSqlDelete(query, context);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }
        

        public static async Task<int> BatchDeleteAsync<T>(
            this IQueryable<T> query,
            CancellationToken cancellationToken = default) where T : class
        {
            DbContext context = query.GetDbContext();
            (string sql, var sqlParameters) = GetSqlDelete(query, context);
            return await context.Database
                .ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken)
                .ConfigureAwait(false);
        }


        // SELECT [a].[Column1], [a].[Column2], .../r/n
        // FROM [Table] AS [a]/r/n
        // WHERE [a].[Column] = FilterValue
        // --
        // DELETE [a]
        // FROM [Table] AS [a]
        // WHERE [a].[Columns] = FilterValues
        private static (string, List<object>) GetSqlDelete<T>(
            IQueryable<T> query, DbContext context) where T : class
        {
            (string sql,
             string tableAlias,
             string _,
             string topStatement,
             IEnumerable<object> _innerParameters)
                = query.GetBatchSql(context, isUpdate: false);

            var innerParameters = _innerParameters.ToList();
            tableAlias = $"[{tableAlias}]";

            var resultQuery = $"DELETE {topStatement}{tableAlias}{sql}";
            return (resultQuery, innerParameters);
        }
    }
}
