#if EFCORE60

using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query
{
    public class SqlServerBulkQueryableMethodTranslatingExpressionVisitor :
        RelationalBulkQueryableMethodTranslatingExpressionVisitor
    {
        public SqlServerBulkQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            QueryCompilationContext queryCompilationContext,
            IAnonymousExpressionFactory anonymousExpressionFactory)
            : base(dependencies, relationalDependencies, queryCompilationContext, anonymousExpressionFactory)
        {
        }

        protected SqlServerBulkQueryableMethodTranslatingExpressionVisitor(
            SqlServerBulkQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new SqlServerBulkQueryableMethodTranslatingExpressionVisitor(this);

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is TemporalQueryRootExpression queryRootExpression)
            {
                // sql server model validator will throw if entity is mapped to multiple tables
                var table = queryRootExpression.EntityType.GetTableMappings().Single().Table;
                var temporalTableExpression = queryRootExpression switch
                {
                    TemporalAllQueryRootExpression _ => (TemporalTableExpression)new TemporalAllTableExpression(table),
                    TemporalAsOfQueryRootExpression asOf => new TemporalAsOfTableExpression(table, asOf.PointInTime),
                    TemporalBetweenQueryRootExpression between => new TemporalBetweenTableExpression(table, between.From, between.To),
                    TemporalContainedInQueryRootExpression containedIn => new TemporalContainedInTableExpression(
                        table, containedIn.From, containedIn.To),
                    TemporalFromToQueryRootExpression fromTo => new TemporalFromToTableExpression(table, fromTo.From, fromTo.To),
                    _ => throw new InvalidOperationException(queryRootExpression.Print())
                };

                var selectExpression = RelationalDependencies.SqlExpressionFactory.Select(
                    queryRootExpression.EntityType,
                    temporalTableExpression);

                return new ShapedQueryExpression(
                    selectExpression,
                    new RelationalEntityShaperExpression(
                        queryRootExpression.EntityType,
                        new ProjectionBindingExpression(
                            selectExpression,
                            new ProjectionMember(),
                            typeof(ValueBuffer)),
                        false));
            }

            return base.VisitExtension(extensionExpression);
        }
    }

    internal class SqlServerBulkQueryableMethodTranslatingExpressionVisitorFactory :
        IBulkQueryableMethodTranslatingExpressionVisitorFactory,
        IServiceAnnotation<IQueryableMethodTranslatingExpressionVisitorFactory, SqlServerQueryableMethodTranslatingExpressionVisitorFactory>
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;
        private readonly RelationalQueryableMethodTranslatingExpressionVisitorDependencies _relationalDependencies;
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;

        public SqlServerBulkQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            IAnonymousExpressionFactory anonymousExpressionFactory)
        {
            Check.NotNull(dependencies, nameof(dependencies));
            Check.NotNull(relationalDependencies, nameof(relationalDependencies));
            Check.NotNull(anonymousExpressionFactory, nameof(anonymousExpressionFactory));

            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
            _anonymousExpressionFactory = anonymousExpressionFactory;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
            => new SqlServerBulkQueryableMethodTranslatingExpressionVisitor(
                _dependencies,
                _relationalDependencies,
                queryCompilationContext,
                _anonymousExpressionFactory);
    }
}

#endif