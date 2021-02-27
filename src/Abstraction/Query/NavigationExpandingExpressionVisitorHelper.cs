using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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
    }
}
