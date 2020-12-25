using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableBatchInsertIntoExtensions
    {
        public static int BatchInsertInto<T>(
            this IQueryable<T> query,
            DbSet<T> _) where T : class
        {
            var context = query.GetDbContext();
            (var sql, var parameters) = GetSqlSelectInto(query, context);
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }


        public static async Task<int> BatchInsertIntoAsync<T>(
            this IQueryable<T> query,
            DbSet<T> _,
            CancellationToken cancellationToken = default) where T : class
        {
            var context = query.GetDbContext();
            (var sql, var parameters) = GetSqlSelectInto(query, context);
            return await context.Database
                .ExecuteSqlRawAsync(sql, parameters, cancellationToken)
                .ConfigureAwait(false);
        }


        private static List<string> Slice(string selects)
        {
            var slices = new List<string>();
            var exiting = new Stack<char>();

            int j = 0;
            for (int i = 0; i < selects.Length; i++)
            {
                if (selects[i] == '\'')
                {
                    i = selects.IndexOf('\'', i + 1);
                    if (i == -1) throw new NotImplementedException();
                }
                else if (exiting.Count == 0 && selects[i] == ',' && selects[i + 1] == ' ')
                {
                    slices.Add(selects[j..i]);
                    j = i + 2;
                    i++;
                }
                else if ("[(".Contains(selects[i]))
                {
                    exiting.Push(selects[i] == '[' ? ']' : ')');
                }
                else if ("])".Contains(selects[i]))
                {
                    if (exiting.Count == 0 || exiting.Peek() != selects[i])
                        throw new NotImplementedException();
                    exiting.Pop();
                }
            }

            return slices;
        }


        private static (string, IEnumerable<object>) GetSqlSelectInto<T>(
            IQueryable<T> query, DbContext context) where T : class
        {
            (var sqlSelect, var para) = query.ToParametrizedSql(out var selExp);
            var tableInfo = context.GetTableInfo<T>();
            var newLineIndex = sqlSelect.IndexOf(Environment.NewLine);

            // argument slicing
            var slices = Slice(sqlSelect[7..newLineIndex] + ", ");
            if (slices.Count != selExp.Projection.Count)
                throw new NotImplementedException();
            for (int i = 0; i < slices.Count; i++)
                if (slices[i].EndsWith(" AS [" + selExp.Projection[i].Alias + "]"))
                    slices[i] = slices[i].Substring(0, slices[i].LastIndexOf(" AS ["));

            var projectionMembers = selExp
                .Private<IDictionary<ProjectionMember, Expression>>("_projectionMapping");
            var something = new StringBuilder(")\r\nSELECT ");
            var fnames = new StringBuilder($"INSERT INTO [{tableInfo.TableName}] (");

            bool hasComma = false;
            foreach (var (pm, exp) in projectionMembers)
            {
                if (!(exp is ConstantExpression constExp) || constExp.Type != typeof(int))
                    throw new NotImplementedException();
                if (!tableInfo.OutputPropertyColumnNamesDict.TryGetValue(pm.ToString(), out var fieldName))
                    throw new NotImplementedException();

                if (hasComma)
                {
                    something.Append(", ");
                    fnames.Append(", ");
                }

                hasComma = true;
                something.Append(slices[(int)constExp.Value])
                    .Append(" AS [").Append(fieldName).Append(']');
                fnames.Append('[').Append(fieldName).Append(']');
            }

            fnames.Append(something).Append(sqlSelect, newLineIndex, sqlSelect.Length - newLineIndex);
            return (fnames.ToString(), para);
        }
    }
}
