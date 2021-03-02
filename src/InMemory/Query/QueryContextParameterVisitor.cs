using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.InMemory.Query.Internal
{
    public class QueryContextParameterVisitor : ExpressionVisitor
    {
        private const string CompiledQueryParameterPrefix = "__";

        private static readonly MethodInfo translatingGetParameterValue
            = typeof(InMemoryExpressionTranslatingExpressionVisitor)
                .GetTypeInfo().GetDeclaredMethod("GetParameterValue");

        private readonly ParameterExpression[] _excluded;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_excluded.Contains(node)) return node;

            if (node.Name?.StartsWith(CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
            {
                return Expression.Call(
                    translatingGetParameterValue.MakeGenericMethod(node.Type),
                    QueryCompilationContext.QueryContextParameter,
                    Expression.Constant(node.Name));
            }

            return node;
        }

        private QueryContextParameterVisitor(ParameterExpression[] excluded)
        {
            _excluded = excluded;
        }

        public static Expression Process(Expression origin, IEnumerable<ParameterExpression> excluded)
        {
            return new QueryContextParameterVisitor(excluded?.ToArray() ?? Array.Empty<ParameterExpression>()).Visit(origin);
        }
    }
}
