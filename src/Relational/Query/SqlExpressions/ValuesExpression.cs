using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a VALUES fake table in a SQL tree.
    /// </summary>
    public class ValuesExpression : TableExpressionBase
    {
        /// <summary>
        /// Creates an expression for query SQL generation.
        /// </summary>
        /// <param name="origin">The original expression.</param>
        /// <param name="valuesCount">The count of tuples.</param>
        public ValuesExpression(
            ValuesExpression origin,
            int valuesCount)
            : base(origin.Alias)
        {
            RuntimeParameter = origin.RuntimeParameter;
            TupleCount = valuesCount;
            ColumnNames = origin.ColumnNames;
            AnonymousType = origin.AnonymousType;
        }

        /// <summary>
        /// Creates an expression for query compilation.
        /// </summary>
        /// <param name="param">The parameter expression.</param>
        /// <param name="columns">The column names.</param>
        /// <param name="type">The anonymous expression type.</param>
        public ValuesExpression(
            ParameterExpression param,
            IReadOnlyList<string> columns,
            AnonymousExpressionType type)
            : base("cte")
        {
            RuntimeParameter = param.Name;
            ColumnNames = columns;
            AnonymousType = type;
        }

        /// <summary>
        /// Creates an expression for query compilation.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="columns">The column names.</param>
        /// <param name="alias">The table alias.</param>
        public ValuesExpression(
            IReadOnlyList<IReadOnlyList<SqlExpression>> values,
            IReadOnlyList<string> columns,
            string alias = "cte")
            : base(alias)
        {
            ImmediateValues = values;
            ColumnNames = columns;
        }

        /// <summary>
        /// The immediate values.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<SqlExpression>> ImmediateValues { get; }

        /// <summary>
        /// The applied parameter value.
        /// </summary>
        public int? TupleCount { get; }

        /// <summary>
        /// The anonymous expression type.
        /// </summary>
        public AnonymousExpressionType AnonymousType { get; }

        /// <summary>
        /// The runtime parameter expression.
        /// </summary>
        public string RuntimeParameter { get; }

        /// <summary>
        /// The values table column name.
        /// </summary>
        public IReadOnlyList<string> ColumnNames { get; }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            Check.NotNull(visitor, nameof(visitor));
            if (ImmediateValues == null) return this;

            var changed = false;
            var immediateValues = ImmediateValues.ToList();
            for (int i = 0; i < immediateValues.Count; i++)
            {
                immediateValues[i] = visitor.VisitCollection(immediateValues[i], "VisitValues");
                changed = changed || immediateValues[i] != ImmediateValues[i];
            }

            return changed
                ? new ValuesExpression(immediateValues, ColumnNames, Alias)
                : this;
        }

        /// <inheritdoc />
#if EFCORE50 || EFCORE60
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            expressionPrinter.Append("(");

            using (expressionPrinter.Indent())
            {
                if (RuntimeParameter != null)
                {
                    var tupleCount = TupleCount.HasValue
                        ? TupleCount.Value.ToString()
                        : RuntimeParameter + ".Count";

                    expressionPrinter.AppendLine()
                        .Append("VALUES ")
                        .Append($"{tupleCount} tuples of {ColumnNames.Count} columns");
                }
                else if (ImmediateValues != null)
                {
                    expressionPrinter.AppendLine()
                        .Append("VALUES ")
                        .Append($"{ImmediateValues.Count} immediate tuples of {ColumnNames.Count} columns");
                }
            }

            expressionPrinter.AppendLine()
                .Append(") AS ")
                .Append(Alias)
                .Append(" (")
                .VisitCollection(ColumnNames, (p, n) => p.Append(n))
                .Append(")");
        }
    }
}
