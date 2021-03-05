#pragma warning disable IDE0066
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq.Expressions;

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
                Expression result = null;

                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                        when genericMethod == BatchOperationMethods.CreateCommonTable &&
                             methodCallExpression.Arguments[1] is ParameterExpression parameter:

                        result = VisitorHelper.CreateDirect(
                            methodCallExpression,
                            Expression.Call(
                                parameter,
                                parameter.Type.GetMethod("get_Item"),
                                Expression.Constant(0))); ;
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
                        VisitorHelper.RemapBatchUpdateJoin(methodCallExpression, out _, out _));


                case nameof(BatchOperationExtensions.Upsert)
                when genericMethod == BatchOperationMethods.UpsertCollapsed:
                    return Expression.Call(
                        methodCallExpression.Method,
                        Expand(methodCallExpression.Arguments[0]),
                        Expand(methodCallExpression.Arguments[1]),
                        methodCallExpression.Arguments[2],
                        methodCallExpression.Arguments[3]);


                case nameof(MergeJoinExtensions.MergeJoin)
                when genericMethod == MergeJoinExtensions.Queryable:
                    return MergeJoinExpand(methodCallExpression);


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
                var innerJoin = Expression.Call(
                    QueryableMethods.Join.MakeGenericMethod(genericArguments),
                    mergeJoinCall.Arguments);

                var expanded = base.Expand(innerJoin);

                if (expanded is not MethodCallExpression afterJoinSelect
                    || afterJoinSelect.Method.Name != nameof(QueryableMethods.Select)
                    || afterJoinSelect.Method.GetGenericMethodDefinition() != QueryableMethods.Select
                    || afterJoinSelect.Arguments[0] is not MethodCallExpression currentJoin
                    || currentJoin.Method.Name != nameof(QueryableMethods.Join)
                    || currentJoin.Method.GetGenericMethodDefinition() != QueryableMethods.Join)
                    throw TranslateFailed();

                return Expression.Call(
                    afterJoinSelect.Method,
                    Expression.Call(
                        MergeJoinExtensions.Queryable.MakeGenericMethod(currentJoin.Method.GetGenericArguments()),
                        currentJoin.Arguments),
                    afterJoinSelect.Arguments[1]);
            }
        }
    }
}
