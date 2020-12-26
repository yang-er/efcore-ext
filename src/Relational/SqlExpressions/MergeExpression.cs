using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class MergeExpression : Expression, IPrintableExpression
    {
        public IEntityType TargetEntityType { get; internal set; }

        public TableExpression TargetTable { get; internal set; }

        public TableExpressionBase SourceTable { get; internal set; }

        public SqlExpression JoinPredicate { get; internal set; }

        /// <remarks>
        /// When null, no this column.
        /// </remarks>
        public IReadOnlyList<ProjectionExpression> Matched { get; internal set; }

        /// <remarks>
        /// When null, no this column.
        /// </remarks>
        public IReadOnlyList<ProjectionExpression> NotMatchedByTarget { get; internal set; }

        /// <remarks>
        /// When false, no this column.
        /// </remarks>
        public bool NotMatchedBySource { get; internal set; }

        /// <remarks>
        /// When <see cref="SourceTable"/> is a <see cref="ValuesExpression"/>,
        /// we should replace this with that values table without tranversing the expression tree.
        /// </remarks>
        public TableExpressionBase TableChanges { get; internal set; }

        /// <remarks>
        /// When <see cref="SourceTable"/> is a <see cref="ValuesExpression"/>,
        /// we should replace the column name with the correct CLR property name without tranversing the expression tree.
        /// </remarks>
        public Dictionary<string, string> ColumnChanges { get; internal set; }

        /// <inheritdoc />
        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Merge Entity");
        }
    }
}
