using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class AffectedRowsExpression : SqlExpression
    {
        public AffectedRowsExpression()
            : base(typeof(int), RelationalTypeMapping.NullMapping)
        {
        }

        /// <inheritdoc />
#if EFCORE50 || EFCORE60
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            expressionPrinter.Append("affected rows");
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }
    }
}
