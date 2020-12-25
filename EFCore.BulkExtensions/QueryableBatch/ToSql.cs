using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableToSqlExtensions
    {
        private static readonly Func<IQueryProvider, QueryContextDependencies> _loader;
        private static readonly ConcurrentDictionary<Type, TableInfo> _tableInfos;


        static QueryableToSqlExtensions()
        {
            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            var param = Expression.Parameter(typeof(IQueryProvider), "query");
            var queryCompiler = Expression.MakeMemberAccess(
                expression: Expression.Convert(param, typeof(EntityQueryProvider)),
                member: typeof(EntityQueryProvider).GetField("_queryCompiler", bindingFlags));
            var queryContextFactory = Expression.MakeMemberAccess(
                expression: Expression.Convert(queryCompiler, typeof(QueryCompiler)),
                member: typeof(QueryCompiler).GetField("_queryContextFactory", bindingFlags));
            var dependencies = Expression.MakeMemberAccess(
                expression: Expression.Convert(queryContextFactory, typeof(RelationalQueryContextFactory)),
                member: typeof(RelationalQueryContextFactory).GetField("_dependencies", bindingFlags));
            var result = Expression.ConvertChecked(dependencies,
                typeof(DbContext).Assembly.GetType(typeof(QueryContextDependencies).FullName));
            var lambda = Expression.Lambda<Func<IQueryProvider, QueryContextDependencies>>(result, param);
            _loader = lambda.Compile();

            _tableInfos = new ConcurrentDictionary<Type, TableInfo>();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (string, IEnumerable<SqlParameter>) ToParametrizedSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            return ToParametrizedSql(query, out var _);
        }


        internal static (string, IEnumerable<SqlParameter>) ToParametrizedSql<TEntity>(this IQueryable<TEntity> query, out SelectExpression selectExpression) where TEntity : class
        {
            string relationalCommandCacheText = "_relationalCommandCache";
            string selectExpressionText = "_selectExpression";
            string querySqlGeneratorFactoryText = "_querySqlGeneratorFactory";
            string relationalQueryContextText = "_relationalQueryContext";

            string cannotGetText = "Cannot get";

            var enumerator = query.Provider.Execute<IEnumerable<TEntity>>(query.Expression).GetEnumerator();
            var queryContext = enumerator.Private<RelationalQueryContext>(relationalQueryContextText) ?? throw new InvalidOperationException($"{cannotGetText} {relationalQueryContextText}");
            var parameterValues = queryContext.ParameterValues;
            
            string sql;
            IList<SqlParameter> parameters;
            if (enumerator.Private(relationalCommandCacheText) is RelationalCommandCache relationalCommandCache)
            {
                selectExpression = relationalCommandCache.Private<SelectExpression>(selectExpressionText);
                var command = relationalCommandCache.GetRelationalCommand(parameterValues);
                var parameterNames = new HashSet<string>(command.Parameters.Select(p => p.InvariantName));
                sql = command.CommandText;
                parameters = parameterValues.Where(pv => parameterNames.Contains(pv.Key)).Select(pv => new SqlParameter("@" + pv.Key, pv.Value)).ToList();
            }
            else
            {
                selectExpression = enumerator.Private<SelectExpression>(selectExpressionText) ?? throw new InvalidOperationException($"{cannotGetText} {selectExpressionText}");
                IQuerySqlGeneratorFactory factory = enumerator.Private<IQuerySqlGeneratorFactory>(querySqlGeneratorFactoryText) ?? throw new InvalidOperationException($"{cannotGetText} {querySqlGeneratorFactoryText}");

                var sqlGenerator = factory.Create();
                var command = sqlGenerator.GetCommand(selectExpression);
                sql = command.CommandText;
                parameters = parameterValues.Select(pv => new SqlParameter("@" + pv.Key, pv.Value)).ToList();
            }

            return (sql, parameters);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DbContext GetDbContext(this IQueryable query)
        {
            return _loader(query.Provider).StateManager.Context;
        }


        static readonly int SelectStatementLength = "SELECT".Length;


        internal static (string, string, string, string, IEnumerable<object>) GetBatchSql<T>(this IQueryable<T> query, DbContext context, bool isUpdate) where T : class
        {
            var (sqlQuery, innerParameters) = query.ToParametrizedSql();

            string tableAlias = string.Empty;
            string tableAliasSufixAs = string.Empty;
            string topStatement = string.Empty;

            string escapeSymbolEnd = "]"; // SqlServer : PostrgeSql;
            string escapeSymbolStart = "["; // SqlServer : PostrgeSql;
            string tableAliasEnd = sqlQuery.Substring(SelectStatementLength, sqlQuery.IndexOf(escapeSymbolEnd) - SelectStatementLength); // " TOP(10) [table_alias" / " [table_alias" : " table_alias"
            int tableAliasStartIndex = tableAliasEnd.IndexOf(escapeSymbolStart);
            tableAlias = tableAliasEnd.Substring(tableAliasStartIndex + escapeSymbolStart.Length); // "table_alias"
            topStatement = tableAliasEnd.Substring(0, tableAliasStartIndex).TrimStart(); // "TOP(10) " / if TOP not present in query this will be a Substring(0,0) == ""


            int indexFROM = sqlQuery.IndexOf(Environment.NewLine);
            string sql = sqlQuery.Substring(indexFROM, sqlQuery.Length - indexFROM);
            sql = sql.Contains("{") ? sql.Replace("{", "{{") : sql; // Curly brackets have to be escaped:
            sql = sql.Contains("}") ? sql.Replace("}", "}}") : sql; // https://github.com/aspnet/EntityFrameworkCore/issues/8820

            return (sql, tableAlias, tableAliasSufixAs, topStatement, innerParameters);
        }


        public static TableInfo GetTableInfo<T>(
            this DbContext dbContext,
            BulkConfig bulkConfig = null,
            OperationType type = OperationType.Read,
            IList<T> entities = null)
        {
            if (bulkConfig == null)
                return _tableInfos.GetOrAdd(typeof(T), type =>
                    TableInfo.CreateInstance(dbContext, new List<T>(), OperationType.Read, new BulkConfig()));
            return TableInfo.CreateInstance(dbContext, entities, type, bulkConfig);
        }
    }
}
