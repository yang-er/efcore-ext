using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ShaperQueryExpressionReplacingVisitor : ExpressionVisitor
    {
        private readonly SelectExpression _origin, _new;

        public ShaperQueryExpressionReplacingVisitor(SelectExpression origin, SelectExpression @new)
        {
            _origin = origin;
            _new = @new;
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is ProjectionBindingExpression proj && proj.QueryExpression == _origin)
            {
                if (proj.Index.HasValue)
                {
                    return new ProjectionBindingExpression(_new, proj.Index.Value, proj.Type);
                }
                else if (proj.ProjectionMember != null)
                {
                    return new ProjectionBindingExpression(_new, proj.ProjectionMember, proj.Type);
                }
                else if (proj.IndexMap != null)
                {
                    return new ProjectionBindingExpression(_new, proj.IndexMap);
                }
            }

            return node == _origin ? _new : base.VisitExtension(node);
        }
    }
}
