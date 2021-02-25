using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class InMemoryParameterAccessVisitor : ExpressionVisitor
    {
        private const string CompiledQueryParameterPrefix = "__";

        private static readonly MethodInfo translating_getParameterValue
            = typeof(InMemoryExpressionTranslatingExpressionVisitor)
                .GetTypeInfo().GetDeclaredMethod("GetParameterValue");

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name?.StartsWith(CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
            {
                return Expression.Call(
                    translating_getParameterValue.MakeGenericMethod(node.Type),
                    QueryCompilationContext.QueryContextParameter,
                    Expression.Constant(node.Name));
            }

            return base.VisitParameter(node);
        }

        public static Expression Process(Expression origin)
        {
            return new InMemoryParameterAccessVisitor().Visit(origin);
        }
    }
}
