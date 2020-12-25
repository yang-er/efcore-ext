using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Internal
{
    internal class ParameterReplaceVisitor : ExpressionVisitor
    {
        public ParameterReplaceVisitor(params (ParameterExpression, Expression)[] changes)
        {
            Changes = changes.ToDictionary(k => k.Item1, k => k.Item2);
        }

        public Dictionary<ParameterExpression, Expression> Changes { get; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return Changes.GetValueOrDefault(node) ?? node;
        }
    }
}
