#nullable enable
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a MERGE in a SQL tree.
    /// </summary>
    public sealed class MergeExpression : WrappedExpression
    {
        public MergeExpression(
            TableExpression targetTable,
            TableExpressionBase sourceTable,
            SqlExpression joinPredicate,
            IReadOnlyList<ProjectionExpression>? matched,
            IReadOnlyList<ProjectionExpression>? notMatchedByTarget,
            bool notMatchedBySource)
        {
            Check.NotNull(targetTable, nameof(targetTable));
            Check.NotNull(sourceTable, nameof(sourceTable));
            Check.NotNull(joinPredicate, nameof(joinPredicate));
            Check.NullOrHasNoNulls(matched, nameof(matched));
            Check.NullOrHasNoNulls(notMatchedByTarget, nameof(notMatchedByTarget));

            TargetTable = targetTable;
            SourceTable = sourceTable;
            JoinPredicate = joinPredicate;
            Matched = matched;
            NotMatchedByTarget = notMatchedByTarget;
            NotMatchedBySource = notMatchedBySource;
        }

        /// <summary>
        /// The target table to merge into.
        /// </summary>
        public TableExpression TargetTable { get; }

        /// <summary>
        /// The source table to merge from.
        /// </summary>
        public TableExpressionBase SourceTable { get; }

        /// <summary>
        /// The join predicate.
        /// </summary>
        public SqlExpression JoinPredicate { get; }

        /// <summary>
        /// When a row is matched between source and target, update the
        /// columns represented by <see cref="ProjectionExpression"/>.
        /// When <c>null</c>, do nothing.
        /// </summary>
        public IReadOnlyList<ProjectionExpression>? Matched { get; }

        /// <summary>
        /// When a row is not matched by target, insert a new row with
        /// columns represented by <see cref="ProjectionExpression"/>.
        /// When <c>null</c>, do nothing.
        /// </summary>
        public IReadOnlyList<ProjectionExpression>? NotMatchedByTarget { get; }

        /// <summary>
        /// When a row is not matched by source, decide whether to delete this row.
        /// </summary>
        public bool NotMatchedBySource { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            const string CallerName = "VisitMerge";

            using (SearchConditionBooleanGuard.With(visitor, false))
            {
                var targetTable = visitor.VisitAndConvert(TargetTable, CallerName);
                bool changed = targetTable != TargetTable;

                var sourceTable = visitor.VisitAndConvert(SourceTable, CallerName);
                changed = changed || sourceTable != SourceTable;

                SqlExpression joinPredicate;
                using (SearchConditionBooleanGuard.With(visitor, true, false))
                {
                    joinPredicate = visitor.VisitAndConvert(JoinPredicate, CallerName);
                    changed = changed || joinPredicate != JoinPredicate;
                }

                var matched = visitor.VisitCollection(Matched, CallerName);
                changed = changed || matched != Matched;

                var notMatchedByTarget = visitor.VisitCollection(NotMatchedByTarget, CallerName);
                changed = changed || notMatchedByTarget != NotMatchedByTarget;

                return changed
                    ? new MergeExpression(targetTable, sourceTable, joinPredicate, matched, notMatchedByTarget, NotMatchedBySource)
                    : this;
            }
        }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("MERGE INTO ").Visit(TargetTable);
            expressionPrinter.AppendLine().Append("USING ").Visit(SourceTable);
            expressionPrinter.AppendLine().Append("ON ").Visit(JoinPredicate);

            expressionPrinter.AppendLine().Append("WHEN MATCHED");
            using (expressionPrinter.Indent())
            {
                if (Matched != null)
                {
                    expressionPrinter
                        .AppendLine().Append("THEN UPDATE SET ")
                        .VisitCollection(Matched, (p, e) => p.Append(e.Alias).Append(" = ").Visit(e.Expression));
                }
                else
                {
                    expressionPrinter
                        .AppendLine().Append("THEN DO NOTHING");
                }
            }

            expressionPrinter.AppendLine().Append("WHEN NOT MATCHED BY TARGET");
            using (expressionPrinter.Indent())
            {
                if (NotMatchedByTarget != null)
                {
                    expressionPrinter
                        .AppendLine().Append("THEN INSERT (")
                        .VisitCollection(NotMatchedByTarget, (p, e) => p.Append(e.Alias))
                        .Append(") VALUES (")
                        .VisitCollection(NotMatchedByTarget, (p, e) => p.Visit(e.Expression))
                        .Append(")");
                }
                else
                {
                    expressionPrinter
                        .AppendLine().Append("THEN DO NOTHING");
                }
            }

            expressionPrinter.AppendLine().Append("WHEN NOT MATCHED BY SOURCE");
            using (expressionPrinter.Indent())
            {
                expressionPrinter.AppendLine()
                    .Append(NotMatchedBySource ? "THEN DELETE" : "THEN DO NOTHING");
            }
        }
    }
}
