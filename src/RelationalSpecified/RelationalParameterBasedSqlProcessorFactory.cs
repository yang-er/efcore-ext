#if EFCORE50

namespace Microsoft.EntityFrameworkCore.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
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

    public class ValuesExpressionExpansionVisitor : SqlExpressionVisitorV2
    {
        private readonly ValuesExpression[] _toReplace, _replacement;

        public ValuesExpressionExpansionVisitor(ValuesExpression[] toReplace, ValuesExpression[] replacement)
        {
            Check.NotNull(toReplace, nameof(toReplace));
            Check.NotNull(replacement, nameof(replacement));
            Check.DebugAssert(toReplace.Length == replacement.Length, "Should be equal length.");

            _toReplace = toReplace;
            _replacement = replacement;
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            for (int i = 0; i < _toReplace.Length; i++)
            {
                if (valuesExpression == _toReplace[i])
                {
                    return _replacement[i];
                }
            }

            return base.VisitValues(valuesExpression);
        }
    }

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

                case SelectIntoExpression selectInto:
                    return Visit(selectInto);

                case UpsertExpression upsert:
                    return Visit(upsert);
            }

            return base.Visit(tableExpressionBase);
        }

        protected virtual IReadOnlyList<ProjectionExpression> Visit(IReadOnlyList<ProjectionExpression> projections)
        {
            if (projections == null) return null;

            bool changed = false;
            var fields = projections.ToList();
            for (int i = 0; i < fields.Count; i++)
            {
                var newExpr = Visit(fields[i].Expression, allowOptimizedExpansion: true, out _);
                fields[i] = fields[i].Update(newExpr);
                changed = changed || fields[i] != projections[i];
            }

            return changed ? fields : projections;
        }

        protected virtual IReadOnlyList<TableExpressionBase> Visit(IReadOnlyList<TableExpressionBase> origTables)
        {
            bool changed = false;
            var tables = origTables.ToList();
            for (int i = 0; i < tables.Count; i++)
            {
                tables[i] = Visit(tables[i]);
                changed = changed || tables[i] != origTables[i];
            }

            return changed ? tables : origTables;
        }

        protected virtual DeleteExpression Visit(DeleteExpression deleteExpression)
        {
            var mainTable = (TableExpression)Visit(deleteExpression.Table);
            bool changed = mainTable == deleteExpression.Table;

            var joinedTables = Visit(deleteExpression.JoinedTables);
            changed |= joinedTables != deleteExpression.JoinedTables;

            var predicate = Visit(deleteExpression.Predicate, allowOptimizedExpansion: true, out _);
            changed |= predicate != deleteExpression.Predicate;

            if (TryGetBoolConstantValue(predicate) == true)
            {
                predicate = null;
                changed = true;
            }

            return changed
                ? new DeleteExpression(mainTable, predicate, joinedTables)
                : deleteExpression;
        }

        protected virtual UpdateExpression Visit(UpdateExpression updateExpression)
        {
            var expandedTable = updateExpression.ExpandedTable == null ? null : (TableExpression)Visit(updateExpression.ExpandedTable);
            bool changed = expandedTable != updateExpression.ExpandedTable;

            var predicate = Visit(updateExpression.Predicate, allowOptimizedExpansion: true, out _);
            changed |= predicate != updateExpression.Predicate;

            var fields = Visit(updateExpression.Fields);
            changed |= fields == updateExpression.Fields;

            var tables = Visit(updateExpression.Tables);
            changed |= tables == Visit(updateExpression.Tables);

            return changed
                ? new UpdateExpression(updateExpression.Expanded, expandedTable, predicate, fields, tables)
                : updateExpression;
        }

        protected virtual SelectIntoExpression Visit(SelectIntoExpression selectIntoExpression)
        {
            var expression = base.Visit(selectIntoExpression.Expression);
            return expression != selectIntoExpression.Expression
                ? new SelectIntoExpression(selectIntoExpression.TableName, selectIntoExpression.Schema, expression)
                : selectIntoExpression;
        }

        protected virtual UpsertExpression Visit(UpsertExpression upsertExpression)
        {
            var targetTable = (TableExpression)Visit(upsertExpression.TargetTable);
            bool changed = targetTable != upsertExpression.TargetTable;

            var sourceTable = Visit(upsertExpression.SourceTable);
            changed = changed || sourceTable != upsertExpression.SourceTable;

            var onConflictUpdate = Visit(upsertExpression.OnConflictUpdate);
            changed = changed || onConflictUpdate != upsertExpression.OnConflictUpdate;

            var columns = Visit(upsertExpression.Columns);
            changed = changed || columns != upsertExpression.Columns;

            return changed
                ? new UpsertExpression(targetTable, sourceTable, columns, onConflictUpdate, upsertExpression.ConflictConstraintName)
                : upsertExpression;
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

            if (sqlExpression is ExcludedTableColumnExpression excluded)
            {
                nullable = excluded.IsNullable;
                return excluded;
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
