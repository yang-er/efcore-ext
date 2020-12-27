using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class UpsertExpression : Expression, IPrintableExpression, IFakeSubselectExpression
    {
        public IEntityType EntityType { get; internal set; }

        public TableExpression TargetTable { get; internal set; }

        public TableExpressionBase SourceTable { get; internal set; }

        public TableExpression ExcludedTable { get; internal set; }

        public IReadOnlyList<ProjectionExpression> OnConflictUpdate { get; internal set; }

        public IReadOnlyList<ProjectionExpression> Columns { get; internal set; }

        /// <inheritdoc cref="MergeExpression.TableChanges" />
        public TableExpressionBase TableChanges { get; internal set; }

        /// <inheritdoc cref="MergeExpression.ColumnChanges" />
        public Dictionary<string, string> ColumnChanges { get; internal set; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Upsert Entity");
        }

        TableExpressionBase IFakeSubselectExpression.FakeTable => SourceTable;

        IFakeSubselectExpression IFakeSubselectExpression.Update(TableExpressionBase real, SelectExpression fake, Dictionary<string, string> columnMapping)
        {
            SourceTable = real;
            TableChanges = fake;
            ColumnChanges = columnMapping;
            return this;
        }

        void IFakeSubselectExpression.AddUpsertField(bool insert, SqlExpression sqlExpression, string columnName)
        {
            var list = (List<ProjectionExpression>)(insert ? Columns : OnConflictUpdate);
            var proj = RelationalInternals.CreateProjectionExpression(sqlExpression, columnName);
            list.Add(proj);
        }
    }
}
