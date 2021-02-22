using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
#if EFCORE31
    using ThirdParameter = IModel;
#elif EFCORE50
    using ThirdParameter = QueryCompilationContext;
#endif

    public class XysQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public XysQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            ThirdParameter thirdParameter)
            : base(dependencies, relationalDependencies, thirdParameter)
        {
            _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
        }

        public XysQueryableMethodTranslatingExpressionVisitor(
            XysQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
            _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new XysQueryableMethodTranslatingExpressionVisitor(this);

        private static ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType, SelectExpression selectExpression)
            => new ShapedQueryExpression(
                selectExpression,
                new EntityShaperExpression(
                    entityType,
                    new ProjectionBindingExpression(
                        selectExpression,
                        new ProjectionMember(),
                        typeof(ValueBuffer)),
                    false));

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(Bulk.RelationalExtensions))
            {
                if (method.Name == nameof(Bulk.RelationalExtensions.CreateCommonTable)
                    && methodCallExpression.Arguments[1] is ParameterExpression param)
                {
                    var values = new ValuesExpression(param);
                    var shaped = (ShapedQueryExpression)Visit(methodCallExpression.Arguments[0]);
                    var newExpr = (NewExpression)shaped.ShaperExpression;
                    var select = (SelectExpression)shaped.QueryExpression;
                    ((List<TableExpressionBase>)select.Tables)[0] = values;
                    RelationalInternals.CleanSelectIdentifier(select);
                    var mapping = RelationalInternals.AccessProjectionMapping(select);

                    if (mapping.Count != newExpr.Arguments.Count)
                    {
                        throw new NotImplementedException();
                    }

                    var constructorParams = newExpr.Constructor.GetParameters();
                    for (int i = 0; i < mapping.Count; i++)
                    {
                        var currentArg = newExpr.Arguments[i];
                        if (currentArg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert) currentArg = unary.Operand;
                        if (currentArg is ProjectionBindingExpression projectionBinding)
                        {
                            var member = projectionBinding.ProjectionMember;
                            if (!(mapping[member] is SqlConstantExpression origin))
                            {
                                throw new NotImplementedException();
                            }

                            var paramName = constructorParams[i].Name;
                            mapping[member] = RelationalInternals.CreateColumnExpression(
                                paramName, values,
                                origin.Type, origin.TypeMapping,
                                constructorParams[i].ParameterType.IsNullableType());
                        }
                    }

                    return shaped;
                }

            }

            return base.VisitMethodCall(methodCallExpression);
        }
    }

    public class XysQueryableMethodTranslatingExpressionVisitorFactory : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;
        private readonly RelationalQueryableMethodTranslatingExpressionVisitorDependencies _relationalDependencies;

        public XysQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(ThirdParameter thirdParameter)
        {
            return new XysQueryableMethodTranslatingExpressionVisitor(
                _dependencies,
                _relationalDependencies,
                thirdParameter);
        }

        private static readonly Type _parentPreprocessorType = typeof(RelationalQueryableMethodTranslatingExpressionVisitorFactory);

        public static void TryReplace(IServiceCollection services)
        {
            var factory = services
                .Where(s => s.ServiceType == typeof(IQueryableMethodTranslatingExpressionVisitorFactory))
                .ToList();

            if (factory.Count != 1 || factory[0].ImplementationType != _parentPreprocessorType)
                throw new InvalidOperationException($"Implementation of IQueryableMethodTranslatingExpressionVisitorFactory is not supported.");

            services.Replace(ServiceDescriptor.Singleton<IQueryableMethodTranslatingExpressionVisitorFactory, XysQueryableMethodTranslatingExpressionVisitorFactory>());
        }
    }
}
