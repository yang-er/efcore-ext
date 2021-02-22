using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage.Internal;
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
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;

        public XysQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            ThirdParameter thirdParameter,
            IAnonymousExpressionFactory anonymousExpressionFactory)
            : base(dependencies, relationalDependencies, thirdParameter)
        {
            _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
            _anonymousExpressionFactory = anonymousExpressionFactory;
        }

        public XysQueryableMethodTranslatingExpressionVisitor(
            XysQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
            _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
            _anonymousExpressionFactory = parentVisitor._anonymousExpressionFactory;
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new XysQueryableMethodTranslatingExpressionVisitor(this);

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(Bulk.RelationalExtensions))
            {
                if (method.Name == nameof(Bulk.RelationalExtensions.CreateCommonTable)
                    && methodCallExpression.Arguments[1] is ParameterExpression param)
                {
                    var entityType = _anonymousExpressionFactory.GetType(param.Type.GetGenericArguments()[0]);
                    var values = new ValuesExpression(param, entityType.Fields.Select(a => a.Name).ToArray());

                    var select = RelationalInternals.CreateSelectExpression(
                        alias: null,
                        projections: new List<ProjectionExpression>(),
                        tables: new List<TableExpressionBase> { values },
                        groupBy: new List<SqlExpression>(),
                        orderings: new List<OrderingExpression>());

                    var mapping = new Dictionary<ProjectionMember, Expression>();
                    var rootMember = new ProjectionMember();
                    var shaperArguments = new List<Expression>(entityType.Fields.Count);

                    foreach (var field in entityType.Fields)
                    {
                        var projectionMember = rootMember.Append(field.PropertyInfo);
                        mapping[projectionMember] = field.CreateColumn(values);
                        shaperArguments.Add(field.CreateProjectionBinding(select, projectionMember));
                    }

                    select.ReplaceProjectionMapping(mapping);
                    var shaper = entityType.CreateShaper(shaperArguments);
                    return new ShapedQueryExpression(select, shaper);
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }
    }

    public class XysQueryableMethodTranslatingExpressionVisitorFactory : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;
        private readonly RelationalQueryableMethodTranslatingExpressionVisitorDependencies _relationalDependencies;
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;

        public XysQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            IAnonymousExpressionFactory anonymousExpressionFactory)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
            _anonymousExpressionFactory = anonymousExpressionFactory;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(ThirdParameter thirdParameter)
        {
            return new XysQueryableMethodTranslatingExpressionVisitor(
                _dependencies,
                _relationalDependencies,
                thirdParameter,
                _anonymousExpressionFactory);
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
