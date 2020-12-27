#nullable enable
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents an UPDATE in a SQL tree.
    /// </summary>
    public sealed class UpdateExpression : Expression, IPrintableExpression
    {
        public UpdateExpression(
            bool expanded,
            TableExpression? expandedTable,
            SqlExpression? predicate,
            IReadOnlyList<ProjectionExpression> fields,
            IReadOnlyList<TableExpressionBase> tables)
        {
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

            var expandedTable = visitor.VisitAndConvert(ExpandedTable, CallerName);
            bool changed = expandedTable != ExpandedTable;

            var predicate = visitor.VisitAndConvert(Predicate, CallerName);
            changed = changed || predicate != Predicate;

            bool fieldsChanged = false;
            var fields = Fields.ToArray();
            for (int i = 0; i < Fields.Count; i++)
            {
                fields[i] = visitor.VisitAndConvert(Fields[i], CallerName);
                fieldsChanged = fieldsChanged || fields[i] != Fields[i];
            }

            bool tablesChanged = false;
            var tables = Tables.ToArray();
            for (int i = 0; i < Tables.Count; i++)
            {
                tables[i] = visitor.VisitAndConvert(Tables[i], CallerName);
                tablesChanged = tablesChanged || tables[i] != Tables[i];
            }

            changed = changed || fieldsChanged || tablesChanged;
            if (!changed) return this;
            return new UpdateExpression(
                Expanded,
                expandedTable,
                predicate,
                fieldsChanged ? fields : Fields,
                tablesChanged ? tables : Tables);
        }

        /// <inheritdoc />
        public void Print(ExpressionPrinter expressionPrinter)
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

        /// <summary>
        /// Creates the UPDATE expression from SELECT expression.
        /// </summary>
        /// <param name="selectExpression">The select expression.</param>
        /// <param name="entityType">The entity type to update.</param>
        /// <param name="internals">The queryable internal expression.</param>
        /// <returns>The update expression.</returns>
        public static UpdateExpression CreateFromSelect(
            SelectExpression selectExpression,
            IEntityType entityType,
            Expression internals)
        {
            if (!(selectExpression.Tables[0] is TableExpression table))
                throw new NotSupportedException("The query root should be main entity.");
            if (table.Name != entityType.GetTableName())
                throw new NotSupportedException("The query root type mismatch.");
            if (selectExpression.Offset != null || selectExpression.Limit != null)
                throw new NotSupportedException("The query can't have .Take() or .Skip() filters.");
            if ((selectExpression.GroupBy?.Count ?? 0) != 0 || selectExpression.Having != null)
                throw new NotSupportedException("The query can't be aggregated.");

            var projectionMapping = RelationalInternals.AccessProjectionMapping(selectExpression);
            var projections = selectExpression.Projection;
            var setFields = new List<ProjectionExpression>(projectionMapping.Count);

            // Do some replacing here..
            var columnNames = entityType.GetColumns();
            foreach (var (member, _id) in projectionMapping)
            {
                if (!columnNames.TryGetValue(member.ToString(), out var fieldName))
                    throw new NotImplementedException("Projection mapping failed.");

                int id = (int)((ConstantExpression)_id).Value!;
                setFields.Add(RelationalInternals.CreateProjectionExpression(projections[id].Expression, fieldName));
            }

            // check the lost fields when it is all parameters
            if (!(internals is MethodCallExpression methodCall)
                || !(methodCall.Arguments[1] is UnaryExpression unary)
                || !(unary.Operand is LambdaExpression lambda)
                || !(lambda.Body is MemberInitExpression memberInit)
                || memberInit.Bindings.Count < projections.Count)
                throw new NotSupportedException("Translation failed.");

            return new UpdateExpression(
                false,
                null,
                selectExpression.Predicate,
                setFields,
                selectExpression.Tables);
        }
    }
}
