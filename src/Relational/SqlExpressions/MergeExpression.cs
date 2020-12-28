#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a MERGE in a SQL tree.
    /// </summary>
    public sealed class MergeExpression : Expression, IPrintableExpression
    {
        public MergeExpression(
            TableExpression targetTable,
            TableExpressionBase sourceTable,
            SqlExpression joinPredicate,
            IReadOnlyList<ProjectionExpression>? matched,
            IReadOnlyList<ProjectionExpression>? notMatchedByTarget,
            bool notMatchedBySource)
        {
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

            var targetTable = visitor.VisitAndConvert(TargetTable, CallerName);
            bool changed = targetTable != TargetTable;

            var sourceTable = visitor.VisitAndConvert(SourceTable, CallerName);
            changed = changed || sourceTable != SourceTable;
            
            var joinPredicate = visitor.VisitAndConvert(JoinPredicate, CallerName);
            changed = changed || joinPredicate != JoinPredicate;

            bool matchedChanged = false;
            var matched = Matched?.ToList();
            for (int i = 0; matched != null && i < matched.Count; i++)
            {
                matched[i] = visitor.VisitAndConvert(matched[i], CallerName);
                matchedChanged = matchedChanged || matched[i] != Matched![i];
            }

            bool notMatchedByTargetChanged = false;
            var notMatchedByTarget = NotMatchedByTarget?.ToList();
            for (int i = 0; notMatchedByTarget != null && i < notMatchedByTarget.Count; i++)
            {
                notMatchedByTarget[i] = visitor.VisitAndConvert(notMatchedByTarget[i], CallerName);
                notMatchedByTargetChanged = notMatchedByTargetChanged || notMatchedByTarget[i] != NotMatchedByTarget![i];
            }

            changed = changed || matchedChanged || notMatchedByTargetChanged;
            if (!changed) return this;
            return new MergeExpression(
                targetTable,
                sourceTable,
                joinPredicate,
                matchedChanged ? matched : Matched,
                notMatchedByTargetChanged ? notMatchedByTarget : NotMatchedByTarget,
                NotMatchedBySource);
        }

        /// <inheritdoc />
        public void Print(ExpressionPrinter expressionPrinter)
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
