using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    internal class VisitorHelper
    {
        public const ResultCardinality AffectedRows = (ResultCardinality)998244353;

        private static readonly Type NavigationTreeExpressionType
            = typeof(NavigationExpandingExpressionVisitor)
                .GetNestedType("NavigationTreeExpression", BindingFlags.NonPublic);

        private static readonly Type NavigationExpansionExpressionType
            = typeof(NavigationExpandingExpressionVisitor)
                .GetNestedType("NavigationExpansionExpression", BindingFlags.NonPublic);

        private static readonly Type NavigationTreeNodeType
            = typeof(NavigationExpandingExpressionVisitor)
                .GetNestedType("NavigationTreeNode", BindingFlags.NonPublic);

        private static readonly Type EntityReferenceType
            = typeof(NavigationExpandingExpressionVisitor)
                .GetNestedType("EntityReference", BindingFlags.NonPublic);

        private static readonly Func<Expression, Expression, Expression> NavigationExpansionExpressionFactory
            = new Func<Expression<Func<Expression, Expression, Expression>>>(delegate
            {
                var origin = Expression.Parameter(typeof(Expression), "origin");
                var entity = Expression.Parameter(typeof(Expression), "entity");
                var treeExp = typeof(NavigationExpandingExpressionVisitor).GetNestedType("NavigationTreeExpression", BindingFlags.NonPublic);
                var navExp = typeof(NavigationExpandingExpressionVisitor).GetNestedType("NavigationExpansionExpression", BindingFlags.NonPublic);

                var node = Expression.Variable(treeExp, "node");
                var treeLeaf = Expression.New(treeExp.GetConstructors()[0], entity);
                var nodeAssign = Expression.Assign(node, treeLeaf);

                var exp = Expression.New(navExp.GetConstructors()[0], origin, node, node, Expression.Constant("cte", typeof(string)));
                var block = Expression.Block(new[] { node }, nodeAssign, exp);
                return Expression.Lambda<Func<Expression, Expression, Expression>>(block, origin, entity);
            })
            .Invoke().Compile();

        private static readonly Func<Expression, Type> EntityReferenceChecker
            = new Func<Expression<Func<Expression, Type>>>(delegate
            {
                var parameter = Expression.Parameter(typeof(Expression));
                var primary = Expression.Convert(parameter, NavigationExpansionExpressionType);
                var currentTree = Expression.Property(primary, "CurrentTree");
                var currentParameter = Expression.Property(currentTree, "CurrentParameter");
                var resultType = Expression.Property(currentParameter, "Type");

                var isLeaf = Expression.TypeIs(currentTree, NavigationTreeExpressionType);
                var leaf = Expression.Convert(currentTree, NavigationTreeExpressionType);
                var value = Expression.Property(leaf, "Value");
                var isEntityReference = Expression.TypeIs(value, EntityReferenceType);

                var condition = Expression.AndAlso(isLeaf, isEntityReference);
                var nothing = Expression.Constant(null, typeof(Type));
                var conditional = Expression.Condition(condition, resultType, nothing);
                return Expression.Lambda<Func<Expression, Type>>(conditional, parameter);
            })
            .Invoke().Compile();

        public static Expression CreateDirect(Expression origin, Expression entity)
            => NavigationExpansionExpressionFactory.Invoke(origin, entity);

        public static Type GetEntityTypeWithinEntityReference(Expression origin)
            => EntityReferenceChecker.Invoke(origin);

        public static MethodCallExpression RemapBatchUpdateJoin(MethodCallExpression methodCallExpression, out MemberInfo outer, out MemberInfo inner)
        {
            var genericArguments = methodCallExpression.Method.GetGenericArguments();
            var outerType = genericArguments[0];
            var innerType = genericArguments[1];
            var joinKeyType = genericArguments[2];

            var transparentIdentifierType = TransparentIdentifierFactory.Create(outerType, innerType);
            var transparentIdentifierOuterMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Outer");
            var transparentIdentifierInnerMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Inner");
            var transparentIdentifierParameter = Expression.Parameter(transparentIdentifierType, "tree");
            var transparentIdentifierReplacement = new[]
            {
                Expression.Field(transparentIdentifierParameter, transparentIdentifierOuterMemberInfo),
                Expression.Field(transparentIdentifierParameter, transparentIdentifierInnerMemberInfo),
            };

            var outerParameter = Expression.Parameter(outerType, "outer");
            var innerParameter = Expression.Parameter(innerType, "inner");
            var predicate = methodCallExpression.Arguments[5].UnwrapLambdaFromQuote();
            var selector = methodCallExpression.Arguments[4].UnwrapLambdaFromQuote();
            outer = transparentIdentifierOuterMemberInfo;
            inner = transparentIdentifierInnerMemberInfo;

            return Expression.Call(
                QueryableMethods.Select.MakeGenericMethod(transparentIdentifierType, outerType),
                Expression.Call(
                    QueryableMethods.Where.MakeGenericMethod(transparentIdentifierType),
                    Expression.Call(
                        QueryableMethods.Join.MakeGenericMethod(outerType, innerType, joinKeyType, transparentIdentifierType),
                        methodCallExpression.Arguments[0],
                        methodCallExpression.Arguments[1],
                        methodCallExpression.Arguments[2],
                        methodCallExpression.Arguments[3],
                        Expression.Quote(
                            Expression.Lambda(
                                Expression.New(
                                    transparentIdentifierType.GetConstructors().Single(),
                                    new[] { outerParameter, innerParameter },
                                    new[] { transparentIdentifierOuterMemberInfo, transparentIdentifierInnerMemberInfo }),
                                outerParameter,
                                innerParameter))),
                    Expression.Quote(
                        Expression.Lambda(
                            new ReplacingExpressionVisitor(
                                predicate.Parameters.ToArray(),
                                transparentIdentifierReplacement).Visit(predicate.Body),
                            transparentIdentifierParameter))),
                Expression.Quote(
                    Expression.Lambda(
                        new ReplacingExpressionVisitor(
                            selector.Parameters.ToArray(),
                            transparentIdentifierReplacement).Visit(selector.Body),
                        transparentIdentifierParameter)));
        }
    }
}
