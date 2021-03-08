#nullable enable
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents an UPDATE in a SQL tree.
    /// </summary>
    public sealed class UpdateExpression : WrappedExpression
    {
        public UpdateExpression(
            bool expanded,
            TableExpression? expandedTable,
            SqlExpression? predicate,
            IReadOnlyList<ProjectionExpression> fields,
            IReadOnlyList<TableExpressionBase> tables)
        {
            Check.HasNoNulls(fields, nameof(fields));
            Check.HasNoNulls(tables, nameof(tables));

            Expanded = expanded;
            ExpandedTable = expandedTable;
            Predicate = predicate;
            Fields = fields;
            Tables = tables;
        }

        /// <summary>
        /// Whether the table has been taken from the <see cref="Tables"/> to <see cref="ExpandedTable"/>.
        /// </summary>
        public bool Expanded { get; }

        /// <summary>
        /// The primary table to set. When not <see cref="Expanded"/>, this should be null.
        /// </summary>
        public TableExpression? ExpandedTable { get; }

        /// <summary>
        /// The update WHERE part.
        /// </summary>
        public SqlExpression? Predicate { get; }

        /// <summary>
        /// The fields to set in the table.
        /// </summary>
        public IReadOnlyList<ProjectionExpression> Fields { get; }

        /// <summary>
        /// The tables for getting update rows. When not <see cref="Expanded"/>,
        /// the first table must be the main table to update.
        /// </summary>
        public IReadOnlyList<TableExpressionBase> Tables { get; }

        /// <summary>
        /// Expand the update tables for some strange dialect.
        /// </summary>
        /// <returns>The expanded UPDATE expression.</returns>
        public UpdateExpression Expand()
        {
            if (Expanded)
            {
                return this;
            }
            else if (Tables.Count == 1)
            {
                return new UpdateExpression(
                    true, (TableExpression)Tables[0], Predicate,
                    Fields, Array.Empty<TableExpressionBase>());
            }
            else if (Tables[1] is InnerJoinExpression innerJoin)
            {
                var newTables = Tables.Skip(1).ToArray();
                newTables[0] = innerJoin.Table;
                SqlExpression predicate;

                if (Predicate == null)
                    predicate = innerJoin.JoinPredicate;
                else
                    predicate = new SqlBinaryExpression(
                        ExpressionType.AndAlso,
                        Predicate,
                        innerJoin.JoinPredicate,
                        innerJoin.JoinPredicate.Type,
                        innerJoin.JoinPredicate.TypeMapping);

                return new UpdateExpression(
                    true, (TableExpression)Tables[0], predicate,
                    Fields, newTables);
            }
            else
            {
                throw new NotSupportedException(
                    "Translation failed for this kind of entity update. " +
                    "If you'd like to provide more information on this, " +
                    "please contact the plugin author.");
            }
        }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            const string CallerName = "VisitUpdate";

            using (SearchConditionBooleanGuard.With(visitor, false))
            {
                var expandedTable = visitor.VisitAndConvert(ExpandedTable, CallerName);
                bool changed = expandedTable != ExpandedTable;

                SqlExpression? predicate;
                using (SearchConditionBooleanGuard.With(visitor, true, false))
                {
                    predicate = visitor.VisitAndConvert(Predicate, CallerName);
                    changed = changed || predicate != Predicate;
                }

                var fields = visitor.VisitCollection(Fields, CallerName);
                changed = changed || fields != Fields;

                var tables = visitor.VisitCollection(Tables, CallerName);
                changed = changed || tables != Tables;

                return changed
                    ? new UpdateExpression(Expanded, expandedTable, predicate, fields, tables)
                    : this;
            }
        }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("UPDATE SET");

            using (expressionPrinter.Indent())
            {
                expressionPrinter.VisitCollection(
                    Fields,
                    (p, e) => p.AppendLine().Append(e.Alias).Append(" = ").Visit(e.Expression));
            }

            if (Tables.Any())
            {
                expressionPrinter
                    .AppendLine().Append("FROM ")
                    .VisitCollection(Tables, (p, e) => p.Visit(e), p => p.AppendLine());
            }

            if (Predicate != null)
            {
                expressionPrinter
                    .AppendLine().Append("WHERE ")
                    .Visit(Predicate);
            }
        }
    }
}
