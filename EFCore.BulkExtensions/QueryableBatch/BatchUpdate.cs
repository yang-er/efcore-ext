using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableBatchUpdateExtensions
    {
        public static int BatchUpdate<T>(
            this IQueryable<T> query,
            T updateValues,
            List<string> updateColumns = null) where T : class, new()
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlUpdate(query, context, updateValues, updateColumns);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }


        public static int BatchUpdate<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression) where T : class
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlUpdate(query, context, updateExpression);
            return context.Database.ExecuteSqlRaw(sql, sqlParameters);
        }


        public static async Task<int> BatchUpdateAsync<T>(
            this IQueryable<T> query,
            T updateValues,
            List<string> updateColumns = null,
            CancellationToken cancellationToken = default) where T : class, new()
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlUpdate(query, context, updateValues, updateColumns);
            return await context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken).ConfigureAwait(false);
        }


        public static async Task<int> BatchUpdateAsync<T>(
            this IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default) where T : class
        {
            var context = query.GetDbContext();
            var (sql, sqlParameters) = GetSqlUpdate(query, context, updateExpression);
            return await context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken).ConfigureAwait(false);
        }


        // SELECT [a].[Column1], [a].[Column2], .../r/n
        // FROM [Table] AS [a]/r/n
        // WHERE [a].[Column] = FilterValue
        // --
        // UPDATE [a] SET [UpdateColumns] = N'updateValues'
        // FROM [Table] AS [a]
        // WHERE [a].[Columns] = FilterValues
        public static (string, List<object>) GetSqlUpdate<T>(
            IQueryable<T> query,
            DbContext context,
            T updateValues,
            List<string> updateColumns) where T : class, new()
        {
            (string sql,
             string tableAlias,
             string tableAliasSufixAs,
             string topStatement,
             IEnumerable<object> innerParameters)
                = query.GetBatchSql(context, isUpdate: true);
            var sqlParameters = new List<object>(innerParameters);

            string sqlSET = GetSqlSetSegment(
                context, updateValues, updateColumns, sqlParameters);

            var resultQuery = $"UPDATE {topStatement}{tableAlias}{tableAliasSufixAs} {sqlSET}{sql}";
            return (resultQuery, sqlParameters);
        }
        

        public static (string, List<object>) GetSqlUpdate<T>(
            IQueryable<T> query,
            DbContext context,
            Expression<Func<T, T>> expression) where T : class
        {
            (string sql,
             string tableAlias,
             string tableAliasSufixAs,
             string topStatement,
             IEnumerable<object> innerParameters)
                = query.GetBatchSql(context, isUpdate: true);

            var sqlColumns = new StringBuilder();
            var sqlParameters = new List<object>(innerParameters);
            var columnNameValueDict = context.GetTableInfo<T>().PropertyColumnNamesDict;

            void CreateUpdateBody(Expression expression)
            {
                if (expression is MemberInitExpression memberInitExpression)
                {
                    foreach (var item in memberInitExpression.Bindings)
                    {
                        if (item is MemberAssignment assignment)
                        {
                            if (columnNameValueDict.TryGetValue(assignment.Member.Name, out string value))
                                sqlColumns.Append($" [{tableAlias}].[{value}]");
                            else
                                sqlColumns.Append($" [{tableAlias}].[{assignment.Member.Name}]");

                            sqlColumns.Append(" =");

                            CreateUpdateBody(assignment.Expression);

                            if (memberInitExpression.Bindings.IndexOf(item) < (memberInitExpression.Bindings.Count - 1))
                                sqlColumns.Append(" ,");
                        }
                    }
                }
                else if (expression is MemberExpression memberExpression && memberExpression.Expression is ParameterExpression)
                {
                    if (columnNameValueDict.TryGetValue(memberExpression.Member.Name, out string value))
                        sqlColumns.Append($" [{tableAlias}].[{value}]");
                    else
                        sqlColumns.Append($" [{tableAlias}].[{memberExpression.Member.Name}]");
                }
                else if (expression is ConstantExpression constantExpression)
                {
                    var parmName = $"__cp_{sqlParameters.Count}";
                    sqlParameters.Add(new SqlParameter(parmName, constantExpression.Value ?? DBNull.Value));
                    sqlColumns.Append($" @{parmName}");
                }
                else if (expression is UnaryExpression unaryExpression)
                {
                    switch (unaryExpression.NodeType)
                    {
                        case ExpressionType.Convert:
                            // Maybe CAST AS ?
                            CreateUpdateBody(unaryExpression.Operand);
                            break;
                        case ExpressionType.Not:
                            sqlColumns.Append(" ~");
                            CreateUpdateBody(unaryExpression.Operand);
                            break;
                        default:
                            throw new NotImplementedException(expression.NodeType.ToString() + " is not supported yet.");
                    }
                }
                else if (expression is BinaryExpression binaryExpression)
                {
                    CreateUpdateBody(binaryExpression.Left);
                    if (!binaryOpreators.TryGetValue(expression.NodeType, out var fstr))
                        throw new NotImplementedException(expression.NodeType.ToString() + " is not supported yet.");
                    sqlColumns.Append(fstr);
                    CreateUpdateBody(binaryExpression.Right);
                }
                else
                {
                    var subExpression = Expression.Lambda(expression);
                    var value = subExpression.Compile().DynamicInvoke();
                    var parmName = $"__lp_{sqlParameters.Count}";
                    sqlParameters.Add(new SqlParameter(parmName, value ?? DBNull.Value));
                    sqlColumns.Append($" @{parmName}");
                }
            }

            CreateUpdateBody(expression.Body);


            var resultQuery = $"UPDATE {topStatement}{tableAlias}{tableAliasSufixAs} SET {sqlColumns} {sql}";
            return (resultQuery, sqlParameters);
        }


        private static Dictionary<ExpressionType, string> binaryOpreators
            = new Dictionary<ExpressionType, string>
            {
                [ExpressionType.Add] = " +",
                [ExpressionType.Divide] = " /",
                [ExpressionType.Multiply] = " *",
                [ExpressionType.Subtract] = " -",
                [ExpressionType.And] = " &",
                [ExpressionType.Or] = " |",
                [ExpressionType.ExclusiveOr] = " ^"
            };


        public static string GetSqlSetSegment<T>(DbContext context, T updateValues, List<string> updateColumns, List<object> parameters) where T : class, new()
        {
            var tableInfo = context.GetTableInfo<T>();
            string sql = string.Empty;
            Type updateValuesType = typeof(T);
            var defaultValues = new T();
            foreach (var propertyNameColumnName in tableInfo.PropertyColumnNamesDict)
            {
                string propertyName = propertyNameColumnName.Key;
                string columnName = propertyNameColumnName.Value;
                var pArray = propertyName.Split(new char[] { '.' });
                Type lastType = updateValuesType;
                PropertyInfo property = lastType.GetProperty(pArray[0]);
                if (property != null)
                {
                    object propertyUpdateValue = property.GetValue(updateValues);
                    object propertyDefaultValue = property.GetValue(defaultValues);
                    for (int i = 1; i < pArray.Length; i++)
                    {
                        lastType = property.PropertyType;
                        property = lastType.GetProperty(pArray[i]);
                        propertyUpdateValue = propertyUpdateValue != null ? property.GetValue(propertyUpdateValue) : propertyUpdateValue;
                        var lastDefaultValues = lastType.Assembly.CreateInstance(lastType.FullName);
                        propertyDefaultValue = property.GetValue(lastDefaultValues);
                    }

                    if (tableInfo.ConvertibleProperties.ContainsKey(columnName))
                    {
                        propertyUpdateValue = tableInfo.ConvertibleProperties[columnName].ConvertToProvider.Invoke(propertyUpdateValue);
                    }

                    bool isDifferentFromDefault = propertyUpdateValue != null && propertyUpdateValue?.ToString() != propertyDefaultValue?.ToString();
                    if (isDifferentFromDefault || (updateColumns != null && updateColumns.Contains(propertyName)))
                    {
                        sql += $"[{columnName}] = @{columnName}, ";
                        propertyUpdateValue = propertyUpdateValue ?? DBNull.Value;
                        parameters.Add(new SqlParameter($"@{columnName}", propertyUpdateValue));
                    }
                }
            }
            if (string.IsNullOrEmpty(sql))
            {
                throw new InvalidOperationException("SET Columns not defined. If one or more columns should be updated to theirs default value use 'updateColumns' argument.");
            }
            sql = sql.Remove(sql.Length - 2, 2); // removes last excess comma and space: ", "
            return $"SET {sql}";
        }
    }
}
