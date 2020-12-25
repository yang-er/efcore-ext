using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public sealed class UpdateExpression : Expression, IPrintableExpression
    {
        public UpdateExpression(
            SelectExpression selectExpression,
            IEntityType table,
            Expression internals)
        {
            if (!(selectExpression.Tables.FirstOrDefault() is TableExpression table1))
                throw new NotSupportedException("The query root should be main entity.");
            if (table1.Name != table.GetTableName())
                throw new NotSupportedException("The query root type mismatch.");

            Table = table1;
            Tables = selectExpression.Tables;
            SetFields = selectExpression.Projection;
            Predicate = selectExpression.Predicate;
            Limit = selectExpression.Limit;

            // Do some replacing here..
            var columnNames = table.GetColumns();
            var proj = EnhancedQuerySqlGenerator._getMapper(selectExpression);
            var list = (List<ProjectionExpression>)selectExpression.Projection;
            int i = 0;
            var projs = new List<ProjectionExpression>();
            var maps = new Dictionary<ProjectionMember, Expression>();

            foreach (var (a, b) in proj)
            {
                if (!columnNames.TryGetValue(a.ToString(), out var fieldName))
                    throw new NotImplementedException();
                int id = (int)((ConstantExpression)b).Value;
                maps.Add(a, Constant(i++));
                projs.Add(new ProjectionExpression(list[id].Expression, fieldName));
            }

            list.Clear();
            list.AddRange(projs);
            selectExpression.ReplaceProjectionMapping(maps);

            // check the lost fields when it is all parameters
            var caller = ((internals as MethodCallExpression).Arguments[1] as UnaryExpression).Operand;
            if (caller is MemberInitExpression mie && mie.Bindings.Count > list.Count)
                throw new NotSupportedException("Translation failed.");
        }

        public TableExpression Table { get; }

        public SqlExpression Predicate { get; }

        public SqlExpression Limit { get; }

        public IReadOnlyList<ProjectionExpression> SetFields { get; }

        public IReadOnlyList<TableExpressionBase> Tables { get; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.AppendLine("Column Setting");
        }
    }
}
