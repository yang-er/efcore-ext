using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class FakeSelectReplacingVisitor : SqlExpressionVisitorV2
    {
        private readonly TableExpression _excludedTable;

        public FakeSelectReplacingVisitor(TableExpression excludedTable)
        {
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

            else
                return base.VisitColumn(columnExpression);
        }
    }
}
