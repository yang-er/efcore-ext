using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;

#if EFCORE31
using ThirdParameter = Microsoft.EntityFrameworkCore.Metadata.IModel;
#elif EFCORE50 || EFCORE60
using ThirdParameter = Microsoft.EntityFrameworkCore.Query.QueryCompilationContext;
#endif

namespace Microsoft.EntityFrameworkCore.InMemory.Query.Internal
{
    public class InMemoryBulkQueryableMethodTranslatingExpressionVisitor : InMemoryQueryableMethodTranslatingExpressionVisitor
    {
        private static readonly MethodInfo _getParameterValueMethodInfo
            = typeof(InMemoryExpressionTranslatingExpressionVisitor)
                .GetTypeInfo().GetDeclaredMethod("GetParameterValue");

        private static readonly MethodInfo _enumerableSelect
            = new Func<IEnumerable<object>, Func<object, object>, IEnumerable<object>>(Enumerable.Select)
                .GetMethodInfo().GetGenericMethodDefinition();

#if EFCORE31

        private static readonly MethodInfo _tryReadValueMethodInfo
            = EntityMaterializerSource.TryReadValueMethod;

        private void UpdateQueryExpression(InMemoryQueryExpression queryExpression, Expression serverQuery)
            => queryExpression.ServerQueryExpression = serverQuery;
            
        private void UpdateShaperExpression(ref ShapedQueryExpression shapedQueryExpression, Expression shaperExpression)
            => shapedQueryExpression.ShaperExpression = shaperExpression;

#elif EFCORE50 || EFCORE60

        private static readonly MethodInfo _tryReadValueMethodInfo
            = ExpressionExtensions.ValueBufferTryReadValueMethod;

        private void UpdateQueryExpression(InMemoryQueryExpression queryExpression, Expression serverQuery)
            => queryExpression.UpdateServerQueryExpression(serverQuery);

        private void UpdateShaperExpression(ref ShapedQueryExpression shapedQueryExpression, Expression shaperExpression)
            => shapedQueryExpression = shapedQueryExpression.UpdateShaperExpression(shaperExpression);

#endif

        public InMemoryBulkQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            ThirdParameter thirdParameter)
            : base(dependencies, thirdParameter)
        {
        }

        protected InMemoryBulkQueryableMethodTranslatingExpressionVisitor(
            InMemoryBulkQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new InMemoryBulkQueryableMethodTranslatingExpressionVisitor(this);

        protected virtual ShapedQueryExpression TranslateCommonTable(Expression neededShaped, Expression commonTable, Type projectedType)
        {
            if (neededShaped is not ShapedQueryExpression shapedQueryExpression)
            {
                return null;
            }

            var queryExpression = (InMemoryQueryExpression)shapedQueryExpression.QueryExpression;
            var oneOfProjectedType = Expression.Parameter(projectedType, "cte");
            var root = new ProjectionMember();
            var valueBufferValues = new List<Expression>();
            var projections = new Dictionary<ProjectionMember, Expression>();
            var shaperArguments = new List<Expression>();

            foreach (var member in projectedType.GetProperties())
            {
                valueBufferValues.Add(
                    Expression.Convert(
                        Expression.Property(oneOfProjectedType, member),
                        typeof(object)));

                projections.Add(
                    root.Append(member),
                    Expression.Call(
                        _tryReadValueMethodInfo.MakeGenericMethod(member.PropertyType),
                        queryExpression.CurrentParameter,
                        Expression.Constant(projections.Count),
                        Expression.Constant(null, typeof(IPropertyBase))));

                shaperArguments.Add(
                    new ProjectionBindingExpression(
                        queryExpression,
                        root.Append(member),
                        member.PropertyType));
            }

            var serverQueryExpression =
                Expression.Call(
                    _enumerableSelect.MakeGenericMethod(projectedType, typeof(ValueBuffer)),
                    QueryContextParameterVisitor.Process(commonTable, Enumerable.Empty<ParameterExpression>()),
                    Expression.Lambda(
                        Expression.New(
                            typeof(ValueBuffer).GetConstructors()[0],
                            Expression.NewArrayInit(
                                typeof(object),
                                valueBufferValues)),
                        oneOfProjectedType));

            var shaperExpression =
                Expression.New(
                    projectedType.GetConstructors()[0],
                    shaperArguments,
                    projectedType.GetProperties());

            queryExpression.ReplaceProjectionMapping(projections);
            UpdateQueryExpression(queryExpression, serverQueryExpression);
            UpdateShaperExpression(ref shapedQueryExpression, shaperExpression);
            return shapedQueryExpression;
        }

        private struct NullableBox<T>
        {
            public T Entity;

            public bool IsNotNull;

            public NullableBox(T entity, bool isNotNull)
            {
                Entity = entity;
                IsNotNull = isNotNull;
            }
        }

        protected virtual ShapedQueryExpression TranslateMergeJoin(
            Expression outerOriginal,
            Expression innerOriginal,
            LambdaExpression outerKeySelector,
            LambdaExpression innerKeySelector,
            LambdaExpression resultSelector,
            LambdaExpression finalizeSelector)
        {
            // Just check the finalizer is correctly processed.
            if (finalizeSelector.Body is not NewExpression finalizeNew ||
                finalizeNew.Arguments.Count != 2 ||
                finalizeNew.Arguments[0] is not MemberExpression finalizeNewLeft ||
                finalizeNew.Arguments[1] is not MemberExpression finalizeNewRight ||
                finalizeNewLeft.Member.Name != "Outer" ||
                finalizeNewRight.Member.Name != "Inner")
                return null;

            // Use one more field to mark whether the inner one is really null?
            // new ValueBuffer( new [] { null, null, null, ... } )

            var outerType = resultSelector.Parameters[0].Type;
            var innerType = resultSelector.Parameters[1].Type;
            var outerInnerTransIdType = resultSelector.Body.Type;
            var innerBoxedType = typeof(NullableBox<>).MakeGenericType(innerType);
            var innerBoxedParameter = Expression.Parameter(innerBoxedType, "innerBoxed");
            var innerBoxedEntityFI = innerBoxedType.GetField(nameof(NullableBox<object>.Entity));
            var innerBoxedIsNotNullFI = innerBoxedType.GetField(nameof(NullableBox<object>.IsNotNull));
            var innerBoxedAccess = Expression.Field(innerBoxedParameter, innerBoxedEntityFI);
            var innerBoxedIsNotNull = Expression.Field(innerBoxedParameter, innerBoxedIsNotNullFI);
            var outerInnerBoxedTransIdType = TransparentIdentifierFactory.Create(outerType, innerBoxedType);
            var outerInnerBoxedTransIdParam = Expression.Parameter(outerInnerBoxedTransIdType, "ti");
            var outerInnerBoxedTransIdOuterFI = outerInnerBoxedTransIdType.GetField("Outer");
            var outerInnerBoxedTransIdInnerFI = outerInnerBoxedTransIdType.GetField("Inner");
            var outerInnerBoxedTransIdAccess = Expression.Field(outerInnerBoxedTransIdParam, outerInnerBoxedTransIdInnerFI);

            innerOriginal = Expression.Call(
                QueryableMethods.Select.MakeGenericMethod(innerType, innerBoxedType),
                innerOriginal,
                Expression.Quote(
                    Expression.Lambda(
                        Expression.New(
                            innerBoxedType.GetConstructors().Single(),
                            new Expression[] { resultSelector.Parameters[1], Expression.Constant(true) },
                            new MemberInfo[] { innerBoxedEntityFI, innerBoxedIsNotNullFI }),
                        resultSelector.Parameters[1])));

            innerKeySelector = Expression.Lambda(
                ReplacingExpressionVisitor.Replace(
                    innerKeySelector.Parameters[0],
                    innerBoxedAccess,
                    innerKeySelector.Body),
                innerBoxedParameter);

            resultSelector = Expression.Lambda(
                Expression.New(
                    outerInnerBoxedTransIdType.GetConstructors()[0],
                    new Expression[] { resultSelector.Parameters[0], innerBoxedParameter },
                    new[] { outerInnerBoxedTransIdOuterFI, outerInnerBoxedTransIdInnerFI }),
                resultSelector.Parameters[0],
                innerBoxedParameter);

            finalizeSelector = Expression.Lambda(
                Expression.New(
                    finalizeNew.Constructor,
                    Expression.Field(outerInnerBoxedTransIdParam, "Outer"),
                    Expression.Condition(
                        Expression.Field(outerInnerBoxedTransIdAccess, innerBoxedIsNotNullFI),
                        Expression.Field(outerInnerBoxedTransIdAccess, innerBoxedEntityFI),
                        Expression.Constant(null, innerBoxedEntityFI.FieldType))),
                outerInnerBoxedTransIdParam);

            if (Visit(outerOriginal) is not ShapedQueryExpression outer ||
                Visit(innerOriginal) is not ShapedQueryExpression inner)
                return null;

            outer = TranslateDefaultIfEmpty(outer, null);
            inner = TranslateDefaultIfEmpty(inner, null);

            var shaped = TranslateJoin(outer, inner, outerKeySelector, innerKeySelector, resultSelector);

            if (shaped == null ||
                shaped.QueryExpression is not InMemoryQueryExpression queryExpression ||
#if EFCORE31 || EFCORE50
                queryExpression.ServerQueryExpression is not MethodCallExpression serverJoin ||
#elif EFCORE60
                queryExpression.ServerQueryExpression is not MethodCallExpression serverJoinSelect ||
                serverJoinSelect.Method.Name != nameof(Enumerable.Select) ||
                serverJoinSelect.Arguments[0] is not MethodCallExpression serverJoin ||
                serverJoinSelect.Arguments[1] is not LambdaExpression anotherSelectNewLambda ||
                anotherSelectNewLambda.Body is not NewExpression anotherSelectNew ||
                anotherSelectNew.Arguments.Count != 1 ||
                anotherSelectNew.Arguments[0] is not NewArrayExpression ||
                anotherSelectNew.Type != typeof(ValueBuffer) ||
#endif
                serverJoin.Method.Name != nameof(Enumerable.Join) ||
                serverJoin.Arguments[0] is not MethodCallExpression outerDefaultIfEmpty ||
                serverJoin.Arguments[1] is not MethodCallExpression innerDefaultIfEmpty ||
                outerDefaultIfEmpty.Method.Name != nameof(Enumerable.DefaultIfEmpty) ||
                innerDefaultIfEmpty.Method.Name != nameof(Enumerable.DefaultIfEmpty) ||
                innerDefaultIfEmpty.Arguments[0] is not MethodCallExpression innerSelect ||
                innerSelect.Method.Name != nameof(Enumerable.Select))
                return null;

            var mergeJoinExpression =
                Expression.Call(
                    MergeJoinExtensions.Enumerable.MakeGenericMethod(serverJoin.Method.GetGenericArguments()),
                    outerDefaultIfEmpty.Arguments[0],
                    innerDefaultIfEmpty.Arguments[0],
                    serverJoin.Arguments[2],
                    serverJoin.Arguments[3],
                    serverJoin.Arguments[4],
                    outerDefaultIfEmpty.Arguments[1],
                    innerDefaultIfEmpty.Arguments[1]);

            UpdateQueryExpression(queryExpression, mergeJoinExpression);
            return TranslateSelect(shaped, finalizeSelector);
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions) ||
                method.DeclaringType == typeof(MergeJoinExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                var genericArguments = method.GetGenericArguments();
                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                    when genericMethod == BatchOperationMethods.CreateCommonTable:
                        return CheckTranslated(TranslateCommonTable(Visit(methodCallExpression.Arguments[0]), methodCallExpression.Arguments[1], genericArguments[1]));


                    case nameof(MergeJoinExtensions.MergeJoin)
                    when genericMethod == MergeJoinExtensions.Queryable2:
                        return CheckTranslated(
                            TranslateMergeJoin(
                                methodCallExpression.Arguments[0],
                                methodCallExpression.Arguments[1],
                                GetLambdaExpressionFromArgument(2),
                                GetLambdaExpressionFromArgument(3),
                                GetLambdaExpressionFromArgument(4),
                                GetLambdaExpressionFromArgument(5)));
                }
            }

            return base.VisitMethodCall(methodCallExpression);

            LambdaExpression GetLambdaExpressionFromArgument(int argumentIndex) =>
                methodCallExpression.Arguments[argumentIndex].UnwrapLambdaFromQuote();

#if EFCORE31
            ShapedQueryExpression CheckTranslated(ShapedQueryExpression translated)
            {
                if (translated == null)
                {
                    throw new InvalidOperationException(
                        CoreStrings.TranslationFailed(methodCallExpression.Print()));
                }

                return translated;
            }
#elif EFCORE50 || EFCORE60
            ShapedQueryExpression CheckTranslated(ShapedQueryExpression translated)
            {
                return translated
                    ?? throw new InvalidOperationException(
                        TranslationErrorDetails == null
                            ? CoreStrings.TranslationFailed(methodCallExpression.Print())
                            : CoreStrings.TranslationFailedWithDetails(
                                methodCallExpression.Print(),
                                TranslationErrorDetails));
            }
#endif
        }
    }

    public class BulkInMemoryQueryableMethodTranslatingExpressionVisitorFactory :
        IBulkQueryableMethodTranslatingExpressionVisitorFactory,
        IServiceAnnotation<IQueryableMethodTranslatingExpressionVisitorFactory, InMemoryQueryableMethodTranslatingExpressionVisitorFactory>
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        public BulkInMemoryQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(ThirdParameter thirdParameter)
        {
            return new InMemoryBulkQueryableMethodTranslatingExpressionVisitor(_dependencies, thirdParameter);
        }
    }
}
