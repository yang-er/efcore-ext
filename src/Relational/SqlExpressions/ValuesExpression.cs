using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections;
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
        public ValuesExpression(
            SqlParameterExpression[,] vals,
            IEnumerable<string> cols,
            string alias) :
            this(vals.AsList(), cols.ToArray(), alias)
        {
            if (ColumnNames.Count != vals.GetLength(1))
            {
                throw new ArgumentException("The count of sql parameters in one row is different from column names.");
            }
        }

        public ValuesExpression(
            IReadOnlyList<IReadOnlyList<SqlParameterExpression>> vals,
            IReadOnlyList<string> cols,
            string alias) :
            base(alias)
        {
            ColumnNames = cols;
            Values = vals;
        }

        public ValuesExpression(
            string alias,
            IList values,
            IReadOnlyList<string> columnNames,
            AnonymousExpressionType anonymousExpressionType,
            ParameterExpression parameterExpression)
            : base(alias)
        {
            RuntimeParameter = parameterExpression;
            ParameterValue = values;
            ColumnNames = columnNames;
            AnonymousType = anonymousExpressionType;
        }

        public ValuesExpression(
            ParameterExpression param,
            IReadOnlyList<string> columns,
            AnonymousExpressionType type)
            : this("cte", null, columns, type, param)
        {
        }

        /// <summary>
        /// The applied parameter value.
        /// </summary>
        public IList ParameterValue { get; }

        /// <summary>
        /// The anonymous expression type.
        /// </summary>
        public AnonymousExpressionType AnonymousType { get; }

        /// <summary>
        /// The runtime parameter expression.
        /// </summary>
        public ParameterExpression RuntimeParameter { get; }

        /// <summary>
        /// The values table column name.
        /// </summary>
        public IReadOnlyList<string> ColumnNames { get; }

        /// <summary>
        /// The value table.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<SqlParameterExpression>> Values { get; }

        /// <inheritdoc cref="Expression.VisitChildren(ExpressionVisitor)" />
        public ValuesExpression VisitInner(ExpressionVisitor visitor)
        {
            if (Values == null) return this;
            List<IReadOnlyList<SqlParameterExpression>> values = null;
            for (int i = 0; i < Values.Count; i++)
            {
                List<SqlParameterExpression> row = null;
                var row2 = Values[i];
                for (int j = 0; j < row2.Count; j++)
                {
                    var item = visitor.VisitAndConvert(row2[j], "VisitValues");
                    if (row2[j] != item)
                    {
                        if (row == null)
                        {
                            row = new List<SqlParameterExpression>(row2.Count);
                            row.AddRange(row2.Take(j));
                        }

                        row.Add(item);
                    }
                    else if (row != null)
                    {
                        row.Add(item);
                    }
                }

                if (row != null)
                {
                    if (values == null)
                    {
                        values = new List<IReadOnlyList<SqlParameterExpression>>(Values.Count);
                        values.AddRange(Values.Take(i));
                    }

                    values.Add(row);
                }
                else if (values != null)
                {
                    values.Add(row2);
                }
            }

            if (values == null) return this;
            return new ValuesExpression(values, ColumnNames, Alias);
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
                expressionPrinter.AppendLine()
                    .Append("VALUES ")
                    .Append($"{Values.Count} tuples of {ColumnNames.Count} columns");
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
