using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc />
    public class BulkQueryCompiler : QueryCompiler, IServiceAnnotation<IQueryCompiler, QueryCompiler>
    {
        private readonly Type _contextType;
        private readonly IEvaluatableExpressionFilter _evaluatableExpressionFilter;
        private readonly IModel _model;

        /// <summary>
        /// Instantiates the <see cref="IQueryCompiler"/>.
        /// </summary>
        public BulkQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDatabase database,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            IModel model)
            : base(queryContextFactory,
                  compiledQueryCache,
                  compiledQueryCacheKeyGenerator,
                  database,
                  logger,
                  currentContext,
                  evaluatableExpressionFilter,
                  model)
        {
            _contextType = currentContext.Context.GetType();
            _evaluatableExpressionFilter = evaluatableExpressionFilter;
            _model = model;
        }

        /// <inheritdoc />
        public override Expression ExtractParameters(
            Expression query,
            IParameterValues parameterValues,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            bool parameterize = true,
            bool generateContextAccessors = false)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.DeclaringType == typeof(BatchOperationMethods))
            {
                var visitor = new ParameterExtractingExpressionVisitorV2(
                    _evaluatableExpressionFilter,
                    parameterValues,
                    _contextType,
                    _model,
                    logger,
                    parameterize,
                    generateContextAccessors);

                return visitor.ExtractParameters(query);
            }
            else
            {
                return base.ExtractParameters(
                    query,
                    parameterValues,
                    logger,
                    parameterize,
                    generateContextAccessors);
            }
        }
    }
}
