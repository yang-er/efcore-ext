#nullable enable
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a DELETE in a SQL tree.
    /// </summary>
    public sealed class DeleteExpression : WrappedExpression
    {
        public DeleteExpression(
            TableExpression table,
            SqlExpression? predicate,
            IReadOnlyList<TableExpressionBase> joinedTables)
        {
            Table = Check.NotNull(table, nameof(table));
            JoinedTables = Check.HasNoNulls(joinedTables, nameof(joinedTables)).ToList();
            Predicate = predicate;
        }

        /// <summary>
        /// Create a DELETE expression from a SELECT expression.
        /// </summary>
        /// <param name="selectExpression">The SELECT expression.</param>
        /// <param name="entityType">The entity type.</param>
        /// <returns>The created DELETE expression.</returns>
        public static DeleteExpression CreateFromSelect(
            SelectExpression selectExpression,
            IEntityType entityType)
        {
            if (!(selectExpression?.Tables?[0] is TableExpression table))
                throw new NotSupportedException("The query root should be main entity.");
            if (table.Name != entityType.GetTableName())
                throw new NotSupportedException("The query root type mismatch.");
            if (selectExpression.Offset != null || selectExpression.Limit != null)
                throw new NotSupportedException("The query can't have .Take() or .Skip() filters.");
            if ((selectExpression.GroupBy?.Count ?? 0) != 0 || selectExpression.Having != null)
                throw new NotSupportedException("The query can't be aggregated.");

            return new DeleteExpression(table, selectExpression.Predicate, selectExpression.Tables);
        }

        /// <summary>
        /// The primary table to operate on.
        /// </summary>
        public TableExpression Table { get; }

        /// <summary>
        /// The <c>WHERE</c> predicate for the <c>DELETE</c>.
        /// </summary>
        public SqlExpression? Predicate { get; }

        /// <summary>
        /// The list of tables sources used to generate the result set.
        /// </summary>
        public IReadOnlyList<TableExpressionBase> JoinedTables { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            const string CallerName = "VisitDelete";

            using (SearchConditionBooleanGuard.With(visitor, false))
            {
                var table = visitor.VisitAndConvert(Table, CallerName);
                bool changed = table != Table;

                SqlExpression? predicate;

                using (SearchConditionBooleanGuard.With(visitor, true, false))
                {
                    predicate = visitor.VisitAndConvert(Predicate, CallerName);
                    changed = changed || predicate != Predicate;
                }

                var joinedTables = JoinedTables.ToList();
                for (int i = 0; i < joinedTables.Count; i++)
                {
                    joinedTables[i] = visitor.VisitAndConvert(JoinedTables[i], CallerName);
                    changed = changed || joinedTables[i] != JoinedTables[i];
                }

                if (!changed) return this;
                return new DeleteExpression(table, predicate, joinedTables);
            }
        }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            Check.NotNull(expressionPrinter, nameof(expressionPrinter));

            expressionPrinter.Append("DELETE ");
            expressionPrinter.Visit(Table);

            if (JoinedTables.Any())
            {
                expressionPrinter.AppendLine().Append("FROM ");

                for (int i = 0; i < JoinedTables.Count; i++)
                {
                    expressionPrinter.Visit(JoinedTables[i]);
                    if (i > 0) expressionPrinter.AppendLine();
                }
            }

            if (Predicate != null)
            {
                expressionPrinter.AppendLine().Append("WHERE ");
                expressionPrinter.Visit(Predicate);
            }
        }
    }
}
