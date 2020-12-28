using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class FakeSelectReplacingVisitor : SqlExpressionVisitorV2
    {
        private readonly SelectExpression _toBeReplaced;
        private readonly TableExpressionBase _replacement;
        private readonly TableExpression _excludedTable = null;
        private readonly IReadOnlyDictionary<string, string> _columnNameMapping;

        public FakeSelectReplacingVisitor(
            SelectExpression toBeReplace,
            TableExpressionBase replacement,
            IReadOnlyDictionary<string, string> columnNameMapping,
            TableExpression excludedTable = null)
        {
            _toBeReplaced = toBeReplace;
            _replacement = replacement;
            _columnNameMapping = columnNameMapping;
            _excludedTable = excludedTable;
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            if (columnExpression.Table == _excludedTable)
                return new ExcludedTableColumnExpression(
                    columnExpression.Name,
                    columnExpression.Type,
                    columnExpression.TypeMapping,
                    columnExpression.IsNullable);

            else if (columnExpression.Table == _toBeReplaced)
                return RelationalInternals.CreateColumnExpression(
                    _columnNameMapping[columnExpression.Name],
                    _replacement,
                    columnExpression.Type,
                    columnExpression.TypeMapping,
                    columnExpression.IsNullable);

            else
                return base.VisitColumn(columnExpression);
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            if (selectExpression == _toBeReplaced) return _replacement;
            return base.VisitSelect(selectExpression);
        }

        public static void Process<TExpression>(
            ref TExpression origin,
            TableExpressionBase fakeTable,
            QueryContext queryContext,
            Func<SelectExpression, QueryContext, (ValuesExpression, IReadOnlyDictionary<string, string>)?> callback,
            TableExpression excludedTable = null)
            where TExpression : Expression
        {
            var fakeTable2 = fakeTable as SelectExpression;
            var (realTable, columnMapping) =
                callback.Invoke(fakeTable2, queryContext)
                    .GetValueOrDefault();

            if (realTable == null && excludedTable == null) return;
            if (realTable == null) fakeTable2 = null;

            origin = new FakeSelectReplacingVisitor(fakeTable2, realTable, columnMapping, excludedTable)
                .VisitAndConvert(origin, null);
        }
    }
}
