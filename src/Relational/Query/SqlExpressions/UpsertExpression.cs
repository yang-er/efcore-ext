#nullable enable
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents an UPSERT in a SQL tree.
    /// </summary>
    public class UpsertExpression : WrappedExpression
    {
        public UpsertExpression(
            TableExpression targetTable,
            TableExpressionBase sourceTable,
            IReadOnlyList<ProjectionExpression> columns,
            IReadOnlyList<ProjectionExpression>? onConflict,
            IKey conflictConstraint)
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.HasNoNulls(columns, nameof(columns));
            Check.NullOrHasNoNulls(onConflict, nameof(onConflict));
            Check.NotNull(conflictConstraint, nameof(conflictConstraint));

            TargetTable = targetTable;
            SourceTable = sourceTable;
            Columns = columns;
            OnConflictUpdate = onConflict;
            ConflictConstraint = conflictConstraint;
        }

        /// <summary>
        /// The target table to INSERT INTO.
        /// </summary>
        public TableExpression TargetTable { get; }

        /// <summary>
        /// The source table to SELECT FROM.
        /// </summary>
        public TableExpressionBase SourceTable { get; }

        /// <summary>
        /// The columns being inserted, whose alias is the corresponding column name.
        /// </summary>
        public IReadOnlyList<ProjectionExpression> Columns { get; }

        /// <summary>
        /// The expressions being updated when conflict.
        /// </summary>
        public IReadOnlyList<ProjectionExpression>? OnConflictUpdate { get; }

        /// <summary>
        /// The conflict constraint name.
        /// </summary>
        public IKey ConflictConstraint { get; }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Upsert Entity");
        }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            const string CallerName = "VisitUpsert";

            var targetTable = visitor.VisitAndConvert(TargetTable, CallerName);
            bool changed = targetTable != TargetTable;

            var sourceTable = visitor.VisitAndConvert(SourceTable, CallerName);
            changed = changed || sourceTable != SourceTable;

            var onConflictUpdate = visitor.VisitCollection(OnConflictUpdate, CallerName);
            changed = changed || onConflictUpdate != OnConflictUpdate;

            var columns = visitor.VisitCollection(Columns, CallerName);
            changed = changed || columns != Columns;

            return changed
                ? new UpsertExpression(targetTable, sourceTable, columns, onConflictUpdate, ConflictConstraint)
                : this;
        }
    }
}
