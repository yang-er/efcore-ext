#nullable enable
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents a SELECT INTO in a SQL tree.
    /// </summary>
    public sealed class SelectIntoExpression : Expression, IPrintableExpression
    {
        public SelectIntoExpression(
            string tableName,
            string schema,
            SelectExpression selectExpression)
        {
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
        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("INSERT INTO ").AppendLine(TableName);
            expressionPrinter.Visit(Expression);
        }

        /// <summary>
        /// Creates an <c>INSERT INTO SELECT</c> expression.
        /// </summary>
        /// <param name="selectExpression">The SELECT expression.</param>
        /// <param name="entityType">The entity type.</param>
        public static SelectIntoExpression CreateFromSelect(SelectExpression selectExpression, IEntityType entityType)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema();

            // Do some replacing here..
            var columnNames = entityType.GetColumns();
            var projection = (List<ProjectionExpression>)selectExpression.Projection;
            var projectionMapping = RelationalInternals.AccessProjectionMapping(selectExpression);
            var newProjections = new List<ProjectionExpression>();
            var newProjectionMapping = new Dictionary<ProjectionMember, Expression>();
            int i = 0;

            foreach (var (member, _id) in projectionMapping)
            {
                if (!columnNames.TryGetValue(member.ToString(), out var fieldName))
                    throw new NotImplementedException("Projection mapping failed.");

                int id = (int)((ConstantExpression)_id).Value!;
                newProjectionMapping.Add(member, Constant(i++));
                newProjections.Add(RelationalInternals.CreateProjectionExpression(projection[id].Expression, fieldName));
            }

#pragma warning disable CS0618
            var newExpression = selectExpression.Update(
                newProjections,
                (List<TableExpressionBase>)selectExpression.Tables,
                selectExpression.Predicate,
                (List<SqlExpression>)selectExpression.GroupBy,
                selectExpression.Having,
                (List<OrderingExpression>)selectExpression.Orderings,
                selectExpression.Limit,
                selectExpression.Offset,
                selectExpression.IsDistinct,
                selectExpression.Alias);
#pragma warning restore CS0618

            newExpression.ReplaceProjectionMapping(newProjectionMapping);
            return new SelectIntoExpression(tableName, schema, newExpression);
        }
    }
}
