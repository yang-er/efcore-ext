#pragma warning disable IDE0066
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query.Internal
{
    /// <inheritdoc />
    public class SupportCommonTableNavigationExpandingExpressionVisitor : NavigationExpandingExpressionVisitor
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public SupportCommonTableNavigationExpandingExpressionVisitor(
#if EFCORE50
            QueryTranslationPreprocessor queryTranslationPreprocessor,
#endif
            QueryCompilationContext queryCompilationContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter)
#if EFCORE31
            : base(queryCompilationContext, evaluatableExpressionFilter)
#elif EFCORE50
            : base(queryTranslationPreprocessor, queryCompilationContext, evaluatableExpressionFilter)
#endif
        {
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                var genericArguments = method.GetGenericArguments();
                Expression result = null;

                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                    when genericMethod == BatchOperationMethods.CreateCommonTable:
                        result = NavigationExpansionExpressionFactory(
                            methodCallExpression,
                            Expression.Call(
                                methodCallExpression.Arguments[1],
                                typeof(IReadOnlyList<>).MakeGenericType(genericArguments[1]).GetMethod("get_Item"),
                                Expression.Constant(0)));
                        break;
                }

                if (result == null)
                {
                    throw new InvalidOperationException(
                        CoreStrings.QueryFailed(methodCallExpression.Print(), GetType().Name));
                }
                else
                {
                    return result;
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        /// <inheritdoc />
        public override Expression Expand(Expression query)
        {
            if (query is not MethodCallExpression methodCallExpression)
            {
                return base.Expand(query);
            }

            var method = methodCallExpression.Method;
            if (method.DeclaringType != typeof(BatchOperationExtensions)
                && method.DeclaringType != typeof(MergeJoinExtensions))
            {
                return base.Expand(query);
            }

            var genericMethod = method.GetGenericMethodDefinition();
            var genericArguments = method.GetGenericArguments();
            switch (method.Name)
            {
                case nameof(BatchOperationExtensions.CreateCommonTable)
                when genericMethod == BatchOperationMethods.CreateCommonTable:
                    return methodCallExpression;


                case nameof(BatchOperationExtensions.BatchDelete)
                when genericMethod == BatchOperationMethods.BatchDelete:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                case nameof(BatchOperationExtensions.BatchUpdate)
                when genericMethod == BatchOperationMethods.BatchUpdate:
                    return BatchUpdateExpand(
                        Expression.Call(
                            QueryableMethods.Select.MakeGenericMethod(genericArguments[0], genericArguments[0]),
                            methodCallExpression.Arguments));


                case nameof(BatchOperationExtensions.BatchInsertInto)
                when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                    return Expression.Call(method, base.Expand(methodCallExpression.Arguments[0]));


                case nameof(BatchOperationExtensions.BatchUpdateJoin)
                when genericMethod == BatchOperationMethods.BatchUpdateJoin:
                    return BatchUpdateExpand(
                        RemapBatchUpdateJoin(methodCallExpression, out _, out _));


                case nameof(BatchOperationExtensions.Upsert)
                when genericMethod == BatchOperationMethods.UpsertCollapsed:
                    return Expression.Call(
                        methodCallExpression.Method,
                        Expand(methodCallExpression.Arguments[0]),
                        Expand(methodCallExpression.Arguments[1]),
                        methodCallExpression.Arguments[2],
                        methodCallExpression.Arguments[3]);


                case nameof(BatchOperationExtensions.Upsert)
                when genericMethod == BatchOperationMethods.UpsertOneCollapsed:
                    return Expression.Call(
                        methodCallExpression.Method,
                        Expand(methodCallExpression.Arguments[0]),
                        methodCallExpression.Arguments[1],
                        methodCallExpression.Arguments[2]);


                case nameof(MergeJoinExtensions.MergeJoin)
                when genericMethod == MergeJoinExtensions.Queryable:
                    return MergeJoinExpand(methodCallExpression);


                case nameof(BatchOperationExtensions.Merge)
                when genericMethod == BatchOperationMethods.MergeCollapsed:
                    return Expression.Call(
                        methodCallExpression.Method,
                        Expand(methodCallExpression.Arguments[0]),
                        Expand(methodCallExpression.Arguments[1]),
                        methodCallExpression.Arguments[2],
                        methodCallExpression.Arguments[3],
                        methodCallExpression.Arguments[4],
                        methodCallExpression.Arguments[5],
                        methodCallExpression.Arguments[6]);


                case nameof(BatchOperationMethods.CreateSingleTuple)
                when genericMethod == BatchOperationMethods.CreateSingleTuple:
                    return Expression.Call(
                        methodCallExpression.Method,
                        Expand(methodCallExpression.Arguments[0]),
                        methodCallExpression.Arguments[1]);


                default:
                    throw TranslateFailed();
            }

            Exception TranslateFailed()
                => new InvalidOperationException(
                    CoreStrings.QueryFailed(query.Print(), GetType().Name));

            Expression BatchUpdateExpand(Expression toUpdate)
            {
                var expanded = base.Expand(toUpdate);

                // TODO: Is type hierarchy affected?
                if (expanded is not MethodCallExpression fakeSelect ||
                    fakeSelect.Method.GetGenericMethodDefinition() != QueryableMethods.Select)
                    throw TranslateFailed();

                var newSelectTypes = fakeSelect.Method.GetGenericArguments();
                return Expression.Call(
                    BatchOperationMethods.BatchUpdateExpanded.MakeGenericMethod(newSelectTypes),
                    fakeSelect.Arguments[0],
                    Expression.Quote(fakeSelect.Arguments[1].UnwrapLambdaFromQuote()));
            }

            Expression MergeJoinExpand(MethodCallExpression mergeJoinCall)
            {
                var expanded = base.Expand(
                    Expression.Call(
                        QueryableMethods.Join.MakeGenericMethod(genericArguments),
                        mergeJoinCall.Arguments));

                if (expanded is not MethodCallExpression afterJoinSelect
                    || afterJoinSelect.Method.Name != nameof(QueryableMethods.Select)
                    || afterJoinSelect.Method.GetGenericMethodDefinition() != QueryableMethods.Select
                    || afterJoinSelect.Arguments[0] is not MethodCallExpression currentJoin
                    || currentJoin.Method.Name != nameof(QueryableMethods.Join)
                    || currentJoin.Method.GetGenericMethodDefinition() != QueryableMethods.Join)
                    throw TranslateFailed();

                var T = currentJoin.Method.GetGenericArguments();
                var T2 = afterJoinSelect.Method.GetGenericArguments();
                Check.DebugAssert(T[3] == T2[0], "Should be the same");

                return Expression.Call(
                    MergeJoinExtensions.Queryable2.MakeGenericMethod(T[0], T[1], T[2], T[3], T2[1]),
                    currentJoin.Arguments[0],
                    currentJoin.Arguments[1],
                    currentJoin.Arguments[2],
                    currentJoin.Arguments[3],
                    currentJoin.Arguments[4],
                    afterJoinSelect.Arguments[1]);
            }
        }

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

        internal static MethodCallExpression RemapBatchUpdateJoin(MethodCallExpression methodCallExpression, out MemberInfo outer, out MemberInfo inner)
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
