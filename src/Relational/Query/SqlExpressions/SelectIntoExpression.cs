#nullable enable
using Microsoft.EntityFrameworkCore.Utilities;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a SELECT INTO in a SQL tree.
    /// </summary>
    public sealed class SelectIntoExpression : WrappedExpression
    {
        public SelectIntoExpression(
            string tableName,
            string schema,
            SelectExpression selectExpression)
        {
            Check.NotNull(tableName, nameof(tableName));
            Check.NotNull(selectExpression, nameof(selectExpression));

            TableName = tableName;
            Schema = schema;
            Expression = selectExpression;
        }

        /// <summary>
        /// The table to INSERT INTO.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// The table to INSERT INTO.
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// The SELECT expression to insert.
        /// </summary>
        public SelectExpression Expression { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var expression = visitor.VisitAndConvert(Expression, "VisitSelectInto");
            if (expression == Expression) return this;
            return new SelectIntoExpression(TableName, Schema, expression);
        }

        /// <inheritdoc />
        protected override void Prints(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("INSERT INTO ").AppendLine(TableName);
            expressionPrinter.Visit(Expression);
        }
    }
}
