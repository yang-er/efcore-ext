using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class MergeExpression : Expression, IPrintableExpression
    {
        public TableExpression TargetTable { get; internal set; }

        public TableExpressionBase SourceTable { get; internal set; }

        public SqlExpression JoinPredicate { get; internal set; }

        public SqlExpression Limit { get; internal set; }

        public IReadOnlyList<ProjectionExpression> Matched { get; internal set; }

        public IReadOnlyList<ProjectionExpression> NotMatchedByTarget { get; internal set; }

        public bool NotMatchedBySource { get; internal set; }

        public TableExpressionBase TableChanges { get; internal set; }

        public Dictionary<string, string> ColumnChanges { get; internal set; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Merge Entity");
        }
    }
}
