using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public sealed class DeleteExpression : Expression, IPrintableExpression
    {
        public DeleteExpression(SelectExpression selectExpression, IEntityType table)
        {
            if (!(selectExpression.Tables.FirstOrDefault() is TableExpression table1))
                throw new NotSupportedException("The query root should be main entity.");
            if (table1.Name != table.GetTableName())
                throw new NotSupportedException("The query root type mismatch.");

            Table = table1;
            Tables = selectExpression.Tables;
            Predicate = selectExpression.Predicate;
            Limit = selectExpression.Limit;
        }

        public TableExpression Table { get; }

        public SqlExpression Predicate { get; }

        public SqlExpression Limit { get; }

        public IReadOnlyList<TableExpressionBase> Tables { get; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Delete Entity");
        }
    }
}
