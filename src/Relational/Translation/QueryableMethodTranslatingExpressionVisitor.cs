using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
        private const string OuterInner = "Microsoft.EntityFrameworkCore.Query.TransparentIdentifierFactory+TransparentIdentifier`2";
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;
        private readonly IModel _model;

        public XysQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            ThirdParameter thirdParameter,
            IAnonymousExpressionFactory anonymousExpressionFactory)
            : base(dependencies, relationalDependencies, thirdParameter)
        {
            _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
            _anonymousExpressionFactory = anonymousExpressionFactory;
            _model = AccessModel(thirdParameter);
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

#if EFCORE31

        private IModel AccessModel(ThirdParameter thirdParameter) => thirdParameter;

        private void UpdateAffectedRowsCardinality(ref ShapedQueryExpression newShaped)
            => newShaped.ResultCardinality = VisitorHelper.AffectedRows;

        private bool IsSameTable(IEntityType entityType, TableExpression tableExpression)
            => entityType.GetTableName() == tableExpression.Name && entityType.GetSchema() == tableExpression.Schema;

        protected virtual ShapedQueryExpression Fail(string message) => null;

#elif EFCORE50

        private IModel AccessModel(ThirdParameter thirdParameter) => thirdParameter.Model;

        private void UpdateAffectedRowsCardinality(ref ShapedQueryExpression newShaped)
            => newShaped = newShaped.UpdateResultCardinality(VisitorHelper.AffectedRows);

        private bool IsSameTable(IEntityType entityType, TableExpression tableExpression)
            => entityType.GetViewOrTableMappings().Single().Table == tableExpression.Table;

        protected virtual ShapedQueryExpression Fail(string message)
        {
            AddTranslationErrorDetails(message);
            return null;
        }

#endif

        protected virtual ShapedQueryExpression TranslateCommonTable(ParameterExpression param)
        {
            var entityType = _anonymousExpressionFactory.GetType(param.Type.GetGenericArguments()[0]);
            var values = new ValuesExpression(param, entityType.Fields.Select(a => a.Name).ToArray(), entityType);

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

        protected virtual ShapedQueryExpression TranslateWrapped(WrappedExpression wrappedExpression)
        {
            var selectExpression = RelationalInternals.CreateSelectExpression(
                alias: null,
                projections: new List<ProjectionExpression>(),
                tables: new List<TableExpressionBase> { wrappedExpression },
                groupBy: new List<SqlExpression>(),
                orderings: new List<OrderingExpression>());

            selectExpression.ReplaceProjectionMapping(
                new Dictionary<ProjectionMember, Expression>
                {
                    [new ProjectionMember()] = new AffectedRowsExpression(),
                });

            var newShaped = new ShapedQueryExpression(
                selectExpression,
                Expression.Convert(
                    new ProjectionBindingExpression(selectExpression, new ProjectionMember(), typeof(int?)),
                    typeof(int)));

            UpdateAffectedRowsCardinality(ref newShaped);
            return newShaped;
        }

        protected virtual ShapedQueryExpression TranslateDelete(Expression shaped)
        {
            if (shaped is not ShapedQueryExpression shapedQueryExpression
                || shapedQueryExpression.QueryExpression is not SelectExpression selectExpression)
            {
                return null;
            }

            if (selectExpression.Offset != null
                || selectExpression.Limit != null
                || (selectExpression.GroupBy?.Count ?? 0) != 0
                || selectExpression.Having != null)
            {
                return Fail("The query can't be aggregated or be with .Take() or .Skip() filters.");
            }

            if (selectExpression?.Tables?[0] is not TableExpression table)
            {
                return Fail("The query root should be main entity.");
            }

            var delete = new DeleteExpression(
                table: table,
                predicate: selectExpression.Predicate,
                joinedTables: selectExpression.Tables);

            return TranslateWrapped(delete);
        }

        protected virtual ShapedQueryExpression TranslateUpdate(Expression shaped, LambdaExpression updateBody)
        {
            if (shaped is not ShapedQueryExpression shapedQueryExpression
                || shapedQueryExpression.QueryExpression is not SelectExpression selectExpression
                || selectExpression.Tables[0] is not TableExpression updateRoot)
            {
                return null;
            }

            if (selectExpression.Offset != null
                || selectExpression.Limit != null
                || (selectExpression.GroupBy?.Count ?? 0) != 0
                || selectExpression.Having != null)
            {
                return Fail("The query can't be aggregated or be with .Take() or .Skip() filters.");
            }

            if (updateBody.Body is not MemberInitExpression memberInitExpression
                || memberInitExpression.NewExpression.Arguments.Count != 0)
            {
                return Fail("Invalid update body expression.");
            }

            var entityType = _model.FindEntityType(updateBody.Body.Type);
            if (entityType == null)
            {
                return Fail("Query tree root type not found.");
            }

            if (!IsSameTable(entityType, updateRoot))
            {
                return Fail("Update query root type mismatch.");
            }

            shaped = TranslateSelect(shapedQueryExpression, updateBody);
            //Check.DebugAssert(shaped == shapedQueryExpression, "Should be the same instance.");
            Check.DebugAssert(selectExpression == shapedQueryExpression.QueryExpression, "Should be the same instance.");

            // Get the concrete update field expression
            var projectionMapping = RelationalInternals.AccessProjectionMapping(selectExpression);
            var setFields = new List<ProjectionExpression>(projectionMapping.Count);
            var columnNames = entityType.GetColumns();

            foreach (var (member, projection) in projectionMapping)
            {
                if (projection is not SqlExpression sqlExpression
                    || !columnNames.TryGetValue(member.ToString(), out var fieldName))
                {
                    throw new NotImplementedException("Unknown projection mapping failed.");
                }

                setFields.Add(RelationalInternals.CreateProjectionExpression(sqlExpression, fieldName));
            }

            var updateExpression = new UpdateExpression(
                expanded: false,
                expandedTable: null,
                predicate: selectExpression.Predicate,
                fields: setFields,
                tables: selectExpression.Tables);

            return TranslateWrapped(updateExpression);
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
                        return CheckTranslated(TranslateCommonTable(param));

                    case nameof(BatchOperationExtensions.BatchDelete)
                        when genericMethod == BatchOperationMethods.BatchDelete:
                        return CheckTranslated(TranslateDelete(Visit(methodCallExpression.Arguments[0])));

                    case nameof(BatchOperationExtensions.BatchUpdate)
                        when genericMethod == BatchOperationMethods.BatchUpdateExpanded:
                        return CheckTranslated(TranslateUpdate(Visit(methodCallExpression.Arguments[0]), methodCallExpression.Arguments[1].UnwrapLambdaFromQuote()));
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
