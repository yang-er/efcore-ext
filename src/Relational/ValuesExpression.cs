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

#if EFCORE50
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            expressionPrinter.AppendLine("Values Table");
        }
    }
}
