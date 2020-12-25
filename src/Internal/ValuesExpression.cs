using System.Collections.Generic;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class ValuesExpression : TableExpressionBase
    {
        public ValuesExpression(SqlParameterExpression[,] vals, IEnumerable<string> cols, string alias) : base(alias)
        {
            Values = vals.AsList();
            ColumnNames = cols.ToList();
        }

        public IReadOnlyList<string> ColumnNames { get; }

        public IReadOnlyList<IReadOnlyList<SqlParameterExpression>> Values { get; }

        public override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.AppendLine("Values Table");
        }
    }
}
