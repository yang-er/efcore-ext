namespace Microsoft.EntityFrameworkCore.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Bulk;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.InMemory.Query.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Storage;

#if EFCORE31
    using ThirdParameter = Metadata.IModel;
#elif EFCORE50
    using ThirdParameter = QueryCompilationContext;
#endif

    public class XysQueryableMethodTranslatingExpressionVisitor : InMemoryQueryableMethodTranslatingExpressionVisitor
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

#elif EFCORE50

        private static readonly MethodInfo _tryReadValueMethodInfo
            = ExpressionExtensions.ValueBufferTryReadValueMethod;

        private void UpdateQueryExpression(InMemoryQueryExpression queryExpression, Expression serverQuery)
            => queryExpression.UpdateServerQueryExpression(serverQuery);

        private void UpdateShaperExpression(ref ShapedQueryExpression shapedQueryExpression, Expression shaperExpression)
            => shapedQueryExpression = shapedQueryExpression.UpdateShaperExpression(shaperExpression);

#endif

        public XysQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            ThirdParameter thirdParameter)
            : base(dependencies, thirdParameter)
        {
        }

        protected XysQueryableMethodTranslatingExpressionVisitor(
            XysQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new XysQueryableMethodTranslatingExpressionVisitor(this);

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

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                    when genericMethod == BatchOperationMethods.CreateCommonTable &&
                         methodCallExpression.Arguments[1] is ParameterExpression param:
                        return CheckTranslated(TranslateCommonTable(Visit(methodCallExpression.Arguments[0]), param));
                }
            }

            return base.VisitMethodCall(methodCallExpression);

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

    public class XysQueryableMethodTranslatingExpressionVisitorFactory :
        IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        public XysQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(ThirdParameter thirdParameter)
        {
            return new XysQueryableMethodTranslatingExpressionVisitor(_dependencies, thirdParameter);
        }
    }
}
