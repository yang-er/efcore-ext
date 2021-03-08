#nullable enable
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System.Collections.Generic;
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
            Check.NotNull(table, nameof(table));
            Check.HasNoNulls(joinedTables, nameof(joinedTables));

            Table = table;
            JoinedTables = joinedTables;
            Predicate = predicate;
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

                var joinedTables = visitor.VisitCollection(JoinedTables, CallerName);
                changed = changed || joinedTables != JoinedTables;

                return changed
                    ? new DeleteExpression(table, predicate, joinedTables)
                    : this;
            }
        }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            Check.NotNull(expressionPrinter, nameof(expressionPrinter));

            expressionPrinter.Append("DELETE ");
            expressionPrinter.Visit(Table);

            if (JoinedTables.Count > 0)
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
