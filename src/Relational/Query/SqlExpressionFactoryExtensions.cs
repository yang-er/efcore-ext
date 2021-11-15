using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

#if EFCORE31 || EFCORE50
using TableReferenceExpression = Microsoft.EntityFrameworkCore.Query.SqlExpressions.TableExpressionBase;
#endif

[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.SqlServer")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.PostgreSql")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.Sqlite")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.MySql")]
namespace Microsoft.EntityFrameworkCore.Query
{
    public static partial class BulkSqlExpressionFactoryExtensions
    {
        private const BindingFlags GeneralBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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
            TableReferenceExpression table,
            Type type,
            RelationalTypeMapping typeMapping,
            bool nullable)
        {
            Check.NotNull(factory, nameof(factory));
            return ColumnExpressionConstructor(name, table, type, typeMapping, nullable);
        }

        public static ColumnExpression Column(
            this ISqlExpressionFactory factory,
            ColumnExpression updateFrom,
            TableExpressionBase table)
        {
            Check.NotNull(factory, nameof(factory));
            return ColumnExpressionConstructor(updateFrom.Name, table, updateFrom.Type, updateFrom.TypeMapping, updateFrom.IsNullable);
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
            var select = SelectExpressionConstructor(alias, projections, tables, groupBy, orderings);

#if EFCORE60
            var refs = (System.Collections.IList)AccessTableReferenceExpression(select);
            foreach (var table in tables)
            {
                refs.Add(new TableReferenceExpression(select, (table as JoinExpressionBase)?.Alias ?? table.Alias).InnerValue);
            }
#endif

            return select;
        }

        public static TableReferenceExpression GetTableReference(
            this SelectExpression selectExpression,
            TableExpressionBase tableExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(tableExpression, nameof(tableExpression));

#if EFCORE31 || EFCORE50
            return tableExpression;
#elif EFCORE60
            return AccessTableReferenceExpression(selectExpression)
                .Select(exp => new TableReferenceExpression(exp))
                .Where(exp => exp.Table == tableExpression)
                .Single();
#endif
        }

        public static IDictionary<ProjectionMember, Expression> GetProjectionMapping(
            this SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            return AccessProjectionMapping(selectExpression);
        }

        public static IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>> GetEntityProjectionCache(
            this SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            return AccessEntityProjectionCache(selectExpression);
        }

        public static void SetPredicate(
            this SelectExpression selectExpression,
            SqlExpression predicate)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            ApplyPredicate(selectExpression, predicate);
        }

        public static void SetHaving(
            this SelectExpression selectExpression,
            SqlExpression predicate)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            ApplyHaving(selectExpression, predicate);
        }

        public static void CopyIdentifiersFrom(
            this SelectExpression selectExpression,
            SelectExpression otherSelectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(otherSelectExpression, nameof(otherSelectExpression));

            ApplyCopyIdentifiersFrom(selectExpression, otherSelectExpression);
        }

        public static void SetAlias(
            this TableExpressionBase tableExpressionBase,
            string alias)
        {
            Check.NotNull(tableExpressionBase, nameof(tableExpressionBase));
            ApplyAlias(tableExpressionBase, alias);
        }

#if EFCORE50 || EFCORE60

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
            Check.NotNull(factory, nameof(factory));
            return factory.Function(name, arguments, returnType, typeMapping);
        }
#endif
    }
}
