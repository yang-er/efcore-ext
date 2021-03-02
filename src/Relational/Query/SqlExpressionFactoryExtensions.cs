using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    public static class BulkSqlExpressionFactoryExtensions
    {
        public static SqlParameterExpression Parameter(
            this ISqlExpressionFactory factory,
            ParameterExpression parameterExpression,
            RelationalTypeMapping typeMapping)
        {
            Check.NotNull(factory, nameof(factory));
            return SqlParameterExpressionConstructor(parameterExpression, typeMapping);
        }

        public static ProjectionExpression Projection(
            this ISqlExpressionFactory factory,
            SqlExpression expression,
            string alias)
        {
            Check.NotNull(factory, nameof(factory));
            return ProjectionExpressionConstructor(expression, alias);
        }

        public static ColumnExpression Column(
            this ISqlExpressionFactory factory,
            string name,
            TableExpressionBase table,
            Type type,
            RelationalTypeMapping typeMapping,
            bool nullable)
        {
            Check.NotNull(factory, nameof(factory));
            return ColumnExpressionConstructor(name, table, type, typeMapping, nullable);
        }

        public static SelectExpression Select(
            this ISqlExpressionFactory factory,
            string alias,
            List<ProjectionExpression> projections,
            List<TableExpressionBase> tables,
            List<SqlExpression> groupBy,
            List<OrderingExpression> orderings)
        {
            Check.NotNull(factory, nameof(factory));
            return SelectExpressionConstructor(alias, projections, tables, groupBy, orderings);
        }

        public static IDictionary<ProjectionMember, Expression> GetProjectionMapping(
            this SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            return AccessProjectionMapping(selectExpression);
        }

        public static void SetPredicate(
            this SelectExpression selectExpression,
            SqlExpression predicate)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            ApplyPredicate(selectExpression, predicate);
        }

        public static void SetAlias(
            this TableExpressionBase tableExpressionBase,
            string alias)
        {
            Check.NotNull(tableExpressionBase, nameof(tableExpressionBase));
            ApplyAlias(tableExpressionBase, alias);
        }

#if EFCORE50

        public static TableExpression Table(
            this ISqlExpressionFactory factory,
            ITableBase table)
        {
            Check.NotNull(factory, nameof(factory));
            return TableExpressionConstructor(table);
        }

#elif EFCORE31

        public static TableExpression Table(
            this ISqlExpressionFactory factory,
            string name,
            string schema,
            string alias)
        {
            Check.NotNull(factory, nameof(factory));
            return TableExpressionConstructor(name, schema, alias);
        }

        public static SqlFunctionExpression Function(
            this ISqlExpressionFactory factory,
            string name,
            IEnumerable<SqlExpression> arguments,
            bool nullable,
            IEnumerable<bool> argumentsPropagateNullability,
            Type returnType,
            RelationalTypeMapping typeMapping = null)
        {
            return factory.Function(name, arguments, returnType, typeMapping);
        }

#endif

        #region Reflection Getting

        private const BindingFlags GeneralBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> SqlParameterExpressionConstructor
            = typeof(SqlParameterExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression>;

        public static readonly Func<SqlExpression, string, ProjectionExpression> ProjectionExpressionConstructor
            = typeof(ProjectionExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<SqlExpression, string, ProjectionExpression>;

        public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> ColumnExpressionConstructor
            = typeof(ColumnExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression>;

#if EFCORE50

        public static readonly Func<ITableBase, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ITableBase, TableExpression>;

#elif EFCORE31

        public static readonly Func<string, string, string, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<string, string, string, TableExpression>;

#endif

        public static readonly Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression> SelectExpressionConstructor
            = typeof(SelectExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>;

        public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = Internals.CreateLambda<SelectExpression, IDictionary<ProjectionMember, Expression>>(
                param => param.AccessField("_projectionMapping"))
            .Compile();

        public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(para1.AccessProperty("Predicate"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<TableExpressionBase, string> ApplyAlias
            = new Func<Expression<Action<TableExpressionBase, string>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
                var para2 = Expression.Parameter(typeof(string), "alias");
                var body = Expression.Assign(para1.AccessProperty("Alias"), para2);
                return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2);
            })
            .Invoke().Compile();

        #endregion
    }
}
