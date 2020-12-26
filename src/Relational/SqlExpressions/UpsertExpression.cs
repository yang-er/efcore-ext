using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class UpsertExpression : Expression, IPrintableExpression
    {
        public IEntityType EntityType { get; internal set; }

        public TableExpression TargetTable { get; internal set; }

        public TableExpressionBase SourceTable { get; internal set; }

        public IReadOnlyList<ProjectionExpression> OnConflictUpdate { get; internal set; }

        public IReadOnlyList<ProjectionExpression> Columns { get; internal set; }

        /// <inheritdoc cref="MergeExpression.TableChanges" />
        public TableExpressionBase TableChanges { get; internal set; }

        /// <inheritdoc cref="MergeExpression.ColumnChanges" />
        public Dictionary<string, string> ColumnChanges { get; internal set; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Upsert Entity");
        }
    }
}
