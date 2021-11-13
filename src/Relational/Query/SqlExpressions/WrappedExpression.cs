namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public abstract class WrappedExpression : TableExpressionBase
    {
        protected WrappedExpression() : base("wrapped") { }

        protected abstract void Prints(ExpressionPrinter expressionPrinter);

#if EFCORE50 || EFCORE60
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            Prints(expressionPrinter);
        }
    }
}
