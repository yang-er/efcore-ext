using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
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

        public void Generate(IRelationalCommandBuilder sql, string paramName)
        {
            if (!TupleCount.HasValue)
            {
                throw new InvalidOperationException(
                    "This instance of values expression is not concrete.");
            }

            for (int i = 0; i < TupleCount.Value; i++)
            {
                if (i != 0) sql.Append(",").AppendLine();
                sql.Append("(");

                for (int j = 0; j < ColumnNames.Count; j++)
                {
                    if (j != 0) sql.Append(", ");
                    sql.Append($"{paramName}_{i}_{j}");
                }

                sql.Append(")");
            }
        }

        /// <inheritdoc />
#if EFCORE50
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            expressionPrinter.Append("(");

            using (expressionPrinter.Indent())
            {
                var tupleCount = TupleCount.HasValue
                    ? TupleCount.Value.ToString()
                    : RuntimeParameter + ".Count";

                expressionPrinter.AppendLine()
                    .Append("VALUES ")
                    .Append($"{tupleCount} tuples of {ColumnNames.Count} columns");
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
