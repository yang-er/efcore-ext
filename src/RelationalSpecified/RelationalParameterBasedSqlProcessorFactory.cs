#if EFCORE50

namespace Microsoft.EntityFrameworkCore.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.EntityFrameworkCore.Bulk;
    using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

#if SQL_SERVER
    using ThisSqlNullabilityProcessor = Microsoft.EntityFrameworkCore.Query.SqlNullabilityProcessor;
    using ThisParameterBasedSqlProcessor = Microsoft.EntityFrameworkCore.SqlServer.Query.Internal.SqlServerParameterBasedSqlProcessor;
    using ThisParameterBasedSqlProcessorFactory = Microsoft.EntityFrameworkCore.SqlServer.Query.Internal.SqlServerParameterBasedSqlProcessorFactory;
#elif POSTGRE_SQL
    using ThisSqlNullabilityProcessor = Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal.NpgsqlSqlNullabilityProcessor;
    using ThisParameterBasedSqlProcessor = Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal.NpgsqlParameterBasedSqlProcessor;
    using ThisParameterBasedSqlProcessorFactory = Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal.NpgsqlParameterBasedSqlProcessorFactory;
#endif

    public class XysSqlNullabilityProcessor : ThisSqlNullabilityProcessor
    {
        private readonly HashSet<ValuesExpression> _valuesExpressions;

        public XysSqlNullabilityProcessor(
            RelationalParameterBasedSqlProcessorDependencies dependencies,
            bool useRelationalNulls,
            HashSet<ValuesExpression> valuesExpressions)
            : base(dependencies, useRelationalNulls)
        {
            _valuesExpressions = valuesExpressions;
        }

        protected override TableExpressionBase Visit(TableExpressionBase tableExpressionBase)
        {
            switch (tableExpressionBase)
            {
                case ValuesExpression values:
                    DoNotCache();
                    _valuesExpressions.Add(values);
                    return values;

                case DeleteExpression delete:
                    return Visit(delete);

                case UpdateExpression update:
                    return Visit(update);
            }

            return base.Visit(tableExpressionBase);
        }

        protected virtual DeleteExpression Visit(DeleteExpression deleteExpression)
        {
            var mainTable = Visit(deleteExpression.Table) as TableExpression;
            bool changed = mainTable == deleteExpression.Table;

            var tables = (List<TableExpressionBase>)deleteExpression.JoinedTables;
            for (var i = 0; i < deleteExpression.JoinedTables.Count; i++)
            {
                var item = deleteExpression.JoinedTables[i];
                var table = Visit(item);
                if (table != item
                    && tables == deleteExpression.JoinedTables)
                {
                    tables = new List<TableExpressionBase>();
                    for (var j = 0; j < i; j++)
                    {
                        tables.Add(deleteExpression.JoinedTables[j]);
                    }

                    changed = true;
                }

                if (tables != deleteExpression.JoinedTables)
                {
                    tables.Add(table);
                }
            }

            var predicate = Visit(deleteExpression.Predicate, allowOptimizedExpansion: true, out _);
            changed |= predicate != deleteExpression.Predicate;

            if (TryGetBoolConstantValue(predicate) == true)
            {
                predicate = null;
                changed = true;
            }

            return changed
                ? new DeleteExpression(mainTable, predicate, tables)
                : deleteExpression;
        }

        protected virtual UpdateExpression Visit(UpdateExpression updateExpression)
        {
            var expandedTable = updateExpression.ExpandedTable == null ? null : (TableExpression)Visit(updateExpression.ExpandedTable);
            bool changed = expandedTable != updateExpression.ExpandedTable;

            var predicate = Visit(updateExpression.Predicate, allowOptimizedExpansion: true, out _);
            changed |= predicate != updateExpression.Predicate;

            bool fieldsChanged = false;
            var fields = updateExpression.Fields.ToList();
            for (int i = 0; i < fields.Count; i++)
            {
                var newExpr = Visit(fields[i].Expression, allowOptimizedExpansion: true, out _);
                fields[i] = fields[i].Update(newExpr);
                fieldsChanged = fieldsChanged || fields[i] != updateExpression.Fields[i];
            }

            bool tablesChanged = false;
            var tables = updateExpression.Tables.ToList();
            for (int i = 0; i < tables.Count; i++)
            {
                tables[i] = Visit(tables[i]);
                tablesChanged = tablesChanged || tables[i] != updateExpression.Tables[i];
            }

            if (!tablesChanged) tables = (List<TableExpressionBase>)updateExpression.Tables;
            if (!fieldsChanged) fields = (List<ProjectionExpression>)updateExpression.Fields;

            return changed || fieldsChanged || tablesChanged
                ? new UpdateExpression(updateExpression.Expanded, expandedTable, predicate, fields, tables)
                : updateExpression;
        }

        private static bool? TryGetBoolConstantValue(SqlExpression expression)
            => expression is SqlConstantExpression constantExpression
                && constantExpression.Value is bool boolValue
                    ? boolValue
                    : (bool?)null;

        protected override SqlExpression VisitCustomSqlExpression(
            SqlExpression sqlExpression,
            bool allowOptimizedExpansion,
            out bool nullable)
        {
            if (sqlExpression is AffectedRowsExpression)
            {
                nullable = false;
                return sqlExpression;
            }

            return base.VisitCustomSqlExpression(
                sqlExpression,
                allowOptimizedExpansion,
                out nullable);
        }
    }

    public class XysParameterBasedSqlProcessor : ThisParameterBasedSqlProcessor
    {
        private readonly HashSet<ValuesExpression> _valuesTables;

        public XysParameterBasedSqlProcessor(
            RelationalParameterBasedSqlProcessorDependencies dependencies,
            bool useRelationalNulls)
            : base(dependencies, useRelationalNulls)
        {
            _valuesTables = new HashSet<ValuesExpression>();
        }

        protected override SelectExpression ProcessSqlNullability(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues,
            out bool canCache)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(parametersValues, nameof(parametersValues));

            return new XysSqlNullabilityProcessor(Dependencies, UseRelationalNulls, _valuesTables)
                .Process(selectExpression, parametersValues, out canCache);
        }

        protected virtual SelectExpression ProcessValuesExpansion(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues)
        {
            if (_valuesTables.Count == 0) return selectExpression;
            var toReplace = _valuesTables.ToArray();
            var replacement = new ValuesExpression[toReplace.Length];

            for (int i = 0; i < toReplace.Length; i++)
            {
                var target = toReplace[i];
                if (parametersValues.TryGetValue(target.RuntimeParameter, out var param)
                    && param is IList lists)
                {
                    replacement[i] = new ValuesExpression(target, lists.Count);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Parameter value corrupted.");
                }
            }

            return (SelectExpression)new ValuesExpressionExpansionVisitor(toReplace, replacement)
                .Visit(selectExpression);
        }

        public override SelectExpression Optimize(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues,
            out bool canCache)
        {
            selectExpression = base.Optimize(selectExpression, parametersValues, out canCache);
            selectExpression = ProcessValuesExpansion(selectExpression, parametersValues);
            return selectExpression;
        }
    }

    public class XysParameterBasedSqlProcessorFactory :
        IRelationalParameterBasedSqlProcessorFactory,
        IServiceAnnotation<IRelationalParameterBasedSqlProcessorFactory, ThisParameterBasedSqlProcessorFactory>
    {
        private readonly RelationalParameterBasedSqlProcessorDependencies _dependencies;

        public XysParameterBasedSqlProcessorFactory(
            RelationalParameterBasedSqlProcessorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));

            _dependencies = dependencies;
        }

        public virtual RelationalParameterBasedSqlProcessor Create(bool useRelationalNulls)
            => new XysParameterBasedSqlProcessor(_dependencies, useRelationalNulls);
    }
}

#endif
