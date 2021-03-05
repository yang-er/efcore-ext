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
#elif EFCORE50
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

        private IEnumerable<IProperty> GetAllPropertiesInHierarchy(IEntityType entityType)
            => entityType.GetTypesInHierarchy().SelectMany(EntityTypeExtensions.GetDeclaredProperties);

#elif EFCORE50

        private static readonly MethodInfo _tryReadValueMethodInfo
            = ExpressionExtensions.ValueBufferTryReadValueMethod;

        private void UpdateQueryExpression(InMemoryQueryExpression queryExpression, Expression serverQuery)
            => queryExpression.UpdateServerQueryExpression(serverQuery);

        private void UpdateShaperExpression(ref ShapedQueryExpression shapedQueryExpression, Expression shaperExpression)
            => shapedQueryExpression = shapedQueryExpression.UpdateShaperExpression(shaperExpression);

        private IEnumerable<IProperty> GetAllPropertiesInHierarchy(IEntityType entityType)
            => entityType.GetAllBaseTypes().Concat(entityType.GetDerivedTypesInclusive())
                .SelectMany(EntityTypeExtensions.GetDeclaredProperties);

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

        protected virtual ShapedQueryExpression TranslateCommonTable(Expression neededShaped, ParameterExpression parameterExpression)
        {
            if (neededShaped is not ShapedQueryExpression shapedQueryExpression)
            {
                return null;
            }

            var queryExpression = (InMemoryQueryExpression)shapedQueryExpression.QueryExpression;
            var projectedType = parameterExpression.Type.GetGenericArguments()[0];
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
                    Expression.Call(
                        _getParameterValueMethodInfo.MakeGenericMethod(parameterExpression.Type),
                        QueryCompilationContext.QueryContextParameter,
                        Expression.Constant(parameterExpression.Name)),
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

        private static readonly Func<InMemoryQueryExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = Internals.CreateLambda<InMemoryQueryExpression, IDictionary<ProjectionMember, Expression>>(
                param => param.AccessField("_projectionMapping"))
            .Compile();

        private Expression GetDefaultExpression(ShapedQueryExpression shapedQueryExpression)
        {
            var queryExpression = (InMemoryQueryExpression)shapedQueryExpression.QueryExpression;
            var _projectionMapping = AccessProjectionMapping(queryExpression);

            var index = 0;
            foreach (var projection in _projectionMapping)
            {
                index += projection.Value is EntityProjectionExpression entityProjection
                    ? GetAllPropertiesInHierarchy(entityProjection.EntityType).Count()
                    : 1;
            }

            return Expression.New(
                typeof(ValueBuffer).GetConstructors().Single(),
                Expression.NewArrayInit(
                    typeof(object),
                    Enumerable.Repeat(
                        Expression.Constant(null, typeof(object)),
                        index)));
        }

        protected virtual ShapedQueryExpression TranslateMergeJoin(
            Expression outerOriginal,
            Expression innerOriginal,
            LambdaExpression outerKeySelector,
            LambdaExpression innerKeySelector,
            LambdaExpression resultSelector)
        {
            if (outerOriginal is not ShapedQueryExpression outer ||
                innerOriginal is not ShapedQueryExpression inner)
                return null;

            var outerDefault = GetDefaultExpression(outer);
            var innerDefault = GetDefaultExpression(inner);

            var shaped = TranslateJoin(outer, inner, outerKeySelector, innerKeySelector, resultSelector);

            if (shaped == null ||
                shaped.QueryExpression is not InMemoryQueryExpression queryExpression ||
                queryExpression.ServerQueryExpression is not MethodCallExpression serverJoin ||
                serverJoin.Method.DeclaringType != typeof(Enumerable) ||
                serverJoin.Method.Name != nameof(Enumerable.Join))
                return null;

            var mergeJoinExpression =
                Expression.Call(
                    MergeJoinExtensions.Enumerable.MakeGenericMethod(serverJoin.Method.GetGenericArguments()),
                    serverJoin.Arguments[0],
                    serverJoin.Arguments[1],
                    serverJoin.Arguments[2],
                    serverJoin.Arguments[3],
                    serverJoin.Arguments[4],
                    outerDefault,
                    innerDefault);

            UpdateQueryExpression(queryExpression, mergeJoinExpression);
            return shaped;
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions) ||
                method.DeclaringType == typeof(MergeJoinExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                    when genericMethod == BatchOperationMethods.CreateCommonTable &&
                         methodCallExpression.Arguments[1] is ParameterExpression param:
                        return CheckTranslated(TranslateCommonTable(Visit(methodCallExpression.Arguments[0]), param));


                    case nameof(MergeJoinExtensions.MergeJoin)
                    when genericMethod == MergeJoinExtensions.Queryable:
                        return CheckTranslated(
                            TranslateMergeJoin(
                                Visit(methodCallExpression.Arguments[0]),
                                Visit(methodCallExpression.Arguments[1]),
                                GetLambdaExpressionFromArgument(2),
                                GetLambdaExpressionFromArgument(3),
                                GetLambdaExpressionFromArgument(4)));
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
#elif EFCORE50
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
