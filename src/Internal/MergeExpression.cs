using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class MergeExpression : Expression, IPrintableExpression
    {
        public TableExpression TargetTable { get; set; }

        public TableExpressionBase SourceTable { get; set; }

        public SqlExpression JoinPredicate { get; set; }

        public SqlExpression Limit { get; set; }

        public IReadOnlyList<ProjectionExpression> Matched { get; set; }

        public IReadOnlyList<ProjectionExpression> NotMatchedByTarget { get; set; }

        public bool NotMatchedBySource { get; set; }

        public TableExpressionBase TableChanges { get; set; }

        public Dictionary<string, string> ColumnChanges { get; set; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Merge Entity");
        }
    }
}
