using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public sealed class SelectIntoExpression : Expression, IPrintableExpression
    {
        public SelectIntoExpression(SelectExpression selectExpression, IEntityType table)
        {
            Expression = selectExpression;

#if EFCORE50
            Table = RelationalInternals.CreateTableExpression(
                table.Model.GetRelationalModel()
                    .FindTable(table.GetTableName(), table.GetSchema()));
#elif EFCORE31
            Table = RelationalInternals.CreateTableExpression(
                table.GetTableName(),
                table.GetSchema(),
                table.GetTableName().ToLower().Substring(0, 1));
#endif

            // Do some replacing here..
            var columnNames = table.GetColumns();
            var proj = RelationalInternals.AccessProjectionMapping(selectExpression);
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
                projs.Add(RelationalInternals.CreateProjectionExpression(list[id].Expression, fieldName));
            }

            list.Clear();
            list.AddRange(projs);
            selectExpression.ReplaceProjectionMapping(maps);
        }

        public TableExpression Table { get; }

        public SelectExpression Expression { get; }

        public void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("Insert Into: ");
            ((IPrintableExpression)Table).Print(expressionPrinter);
            expressionPrinter.AppendLine();
            ((IPrintableExpression)Expression).Print(expressionPrinter);
        }
    }
}
