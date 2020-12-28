#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents an UPSERT in a SQL tree.
    /// </summary>
    public class UpsertExpression : Expression, IPrintableExpression
    {
        public UpsertExpression(
            TableExpression targetTable,
            TableExpressionBase sourceTable,
            IReadOnlyList<ProjectionExpression> columns,
            IReadOnlyList<ProjectionExpression>? onConflict,
            string conflictConstraintName)
        {
            TargetTable = targetTable;
            SourceTable = sourceTable;
            Columns = columns;
            OnConflictUpdate = onConflict;
            ConflictConstraintName = conflictConstraintName;
        }

        /// <summary>
        /// The target table to INSERT INTO.
        /// </summary>
        public TableExpression TargetTable { get; internal set; }

        /// <summary>
        /// The source table to SELECT FROM.
        /// </summary>
        public TableExpressionBase SourceTable { get; internal set; }

        /// <summary>
        /// The columns being inserted, whose alias is the corresponding column name.
        /// </summary>
        public IReadOnlyList<ProjectionExpression> Columns { get; internal set; }

        /// <summary>
        /// The expressions being updated when conflict.
        /// </summary>
        public IReadOnlyList<ProjectionExpression>? OnConflictUpdate { get; internal set; }

        /// <summary>
        /// The conflict constraint name.
        /// </summary>
        public string ConflictConstraintName { get; }

        /// <inheritdoc />
        public void Print(ExpressionPrinter expressionPrinter)
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

            bool onConflictUpdateChanged = false;
            var onConflictUpdate = OnConflictUpdate?.ToList();
            for (int i = 0; OnConflictUpdate != null && onConflictUpdate != null && i < OnConflictUpdate.Count; i++)
            {
                onConflictUpdate[i] = visitor.VisitAndConvert(OnConflictUpdate[i], CallerName);
                onConflictUpdateChanged = onConflictUpdateChanged || onConflictUpdate[i] != OnConflictUpdate[i];
            }

            bool columnsChanged = false;
            var columns = Columns.ToList();
            for (int i = 0; i < columns.Count; i++)
            {
                columns[i] = visitor.VisitAndConvert(Columns[i], CallerName);
                columnsChanged = columnsChanged || columns[i] != Columns[i];
            }

            changed = changed || onConflictUpdateChanged || columnsChanged;
            if (!changed) return this;
            return new UpsertExpression(
                targetTable,
                sourceTable,
                columnsChanged ? columns : Columns,
                onConflictUpdateChanged ? onConflictUpdate : OnConflictUpdate,
                ConflictConstraintName);
        }
    }
}
