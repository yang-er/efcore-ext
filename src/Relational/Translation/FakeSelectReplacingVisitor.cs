using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class FakeSelectReplacingVisitor : SqlExpressionVisitorV2
    {
        private readonly SelectExpression _toBeReplaced;
        private readonly TableExpressionBase _replacement;
        private readonly IReadOnlyDictionary<string, string> _columnNameMapping;

        public FakeSelectReplacingVisitor(
            SelectExpression toBeReplace,
            TableExpressionBase replacement,
            IReadOnlyDictionary<string, string> columnNameMapping)
        {
            _toBeReplaced = toBeReplace;
            _replacement = replacement;
            _columnNameMapping = columnNameMapping;
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            if (columnExpression.Table != _toBeReplaced)
                return base.VisitColumn(columnExpression);
            var newColumnName = _columnNameMapping[columnExpression.Name];

            return RelationalInternals.CreateColumnExpression(
                newColumnName,
                _replacement,
                columnExpression.Type,
                columnExpression.TypeMapping,
                columnExpression.IsNullable);
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            if (selectExpression == _toBeReplaced) return _replacement;
            return base.VisitSelect(selectExpression);
        }
    }
}
