using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class RelationalBulkShapedQueryCompilingExpressionVisitor : RelationalShapedQueryCompilingExpressionVisitor
    {
        private readonly bool _useRelationalNulls;
        private readonly ISet<string> _tags;
        private readonly QueryCompilationContext _queryCompilationContext;

        public RelationalBulkShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            RelationalShapedQueryCompilingExpressionVisitorDependencies relationalDependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, relationalDependencies, queryCompilationContext)
        {
            _tags = queryCompilationContext.Tags;
            _useRelationalNulls = RelationalOptionsExtension.Extract(queryCompilationContext.ContextOptions).UseRelationalNulls;
            _queryCompilationContext = queryCompilationContext;
        }

#if EFCORE31

        private void VerifyNoClientConstant(Expression expression)
        {
        }

        private RelationalCommandCache CreateCommandCache(SelectExpression selectExpression)
        {
            var commandCache = new RelationalCommandCache(
                Dependencies.MemoryCache,
                RelationalDependencies.SqlExpressionFactory,
                RelationalDependencies.ParameterNameGeneratorFactory,
                RelationalDependencies.QuerySqlGeneratorFactory,
                _useRelationalNulls,
                selectExpression);

            var optimizer = new BulkParameterValueBasedSelectExpressionOptimizer(
                RelationalDependencies.SqlExpressionFactory,
                RelationalDependencies.ParameterNameGeneratorFactory,
                _useRelationalNulls);

            typeof(RelationalCommandCache)
                .GetField("_parameterValueBasedSelectExpressionOptimizer", Internals.bindingFlags)
                .SetValue(commandCache, optimizer);

            return commandCache;
        }

#elif EFCORE50

        private RelationalCommandCache CreateCommandCache(SelectExpression selectExpression)
        {
            return new RelationalCommandCache(
                Dependencies.MemoryCache,
                RelationalDependencies.QuerySqlGeneratorFactory,
                RelationalDependencies.RelationalParameterBasedSqlProcessorFactory,
                selectExpression,
                readerColumns: null,
                _useRelationalNulls);
        }

#endif

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            if (extensionExpression is ShapedQueryExpression shapedQueryExpression
                && shapedQueryExpression.ResultCardinality == VisitorHelper.AffectedRows)
            {
                var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;
                VerifyNoClientConstant(shapedQueryExpression.ShaperExpression);
                selectExpression.ApplyTags(_tags);
                var relationalCommandCache = CreateCommandCache(selectExpression);

                return Expression.Call(
                    instance: Expression.New(
                        typeof(RelationalBulkQueryExecutor).GetConstructor(new[] { typeof(RelationalQueryContext), typeof(RelationalCommandCache) }),
                        Expression.Convert(QueryCompilationContext.QueryContextParameter, typeof(RelationalQueryContext)),
                        Expression.Constant(relationalCommandCache)),
                    method: _queryCompilationContext.IsAsync
                        ? typeof(RelationalBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.ExecuteAsync))
                        : typeof(RelationalBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.Execute)));
            }

            return base.VisitExtension(extensionExpression);
        }
    }

    public class RelationalBulkShapedQueryCompilingExpressionVisitorFactory :
        IBulkShapedQueryCompilingExpressionVisitorFactory,
        IServiceAnnotation<IShapedQueryCompilingExpressionVisitorFactory, RelationalShapedQueryCompilingExpressionVisitorFactory>
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
        private readonly RelationalShapedQueryCompilingExpressionVisitorDependencies _relationalDependencies;

        public RelationalBulkShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            RelationalShapedQueryCompilingExpressionVisitorDependencies relationalDependencies,
#if EFCORE50
            IRelationalBulkParameterBasedSqlProcessorFactory parameterBasedSqlProcessorFactory,
#endif
            IBulkQuerySqlGeneratorFactory querySqlGeneratorFactory)
        {
            Check.NotNull(dependencies, nameof(dependencies));
            Check.NotNull(relationalDependencies, nameof(relationalDependencies));

#if EFCORE50
            Check.NotNull(parameterBasedSqlProcessorFactory, nameof(parameterBasedSqlProcessorFactory));
            relationalDependencies = relationalDependencies.With(parameterBasedSqlProcessorFactory);
#endif

            Check.NotNull(querySqlGeneratorFactory, nameof(querySqlGeneratorFactory));
            relationalDependencies = relationalDependencies.With(querySqlGeneratorFactory);

            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));

            return new RelationalBulkShapedQueryCompilingExpressionVisitor(
                _dependencies,
                _relationalDependencies,
                queryCompilationContext);
        }
    }
}
