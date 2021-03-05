using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc />
    public class BulkQueryCompiler : QueryCompiler, IServiceAnnotation<IQueryCompiler, QueryCompiler>
    {
        private readonly Type _contextType;
        private readonly IEvaluatableExpressionFilter _evaluatableExpressionFilter;
        private readonly IModel _model;

        /// <summary>
        /// The method of <see cref="IBulkQueryExecutor.Execute"/>
        /// </summary>
        public static MethodInfo BulkQueryExecutorExecute { get; }
            = typeof(IBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.Execute));

        /// <summary>
        /// The method of <see cref="IBulkQueryExecutor.ExecuteAsync"/>
        /// </summary>
        public static MethodInfo BulkQueryExecutorExecuteAsync { get; }
            = typeof(IBulkQueryExecutor).GetMethod(nameof(IBulkQueryExecutor.ExecuteAsync));

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
                && methodCallExpression.Method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = methodCallExpression.Method.GetGenericMethodDefinition();
                var genericArguments = methodCallExpression.Method.GetGenericArguments();
                switch (methodCallExpression.Method.Name)
                {
                    case nameof(BatchOperationExtensions.BatchInsertInto)
                    when genericMethod == BatchOperationMethods.BatchInsertInto:
                        query = Expression.Call(
                            BatchOperationMethods.BatchInsertIntoCollapsed.MakeGenericMethod(genericArguments),
                            methodCallExpression.Arguments[0]);
                        break;

                    case nameof(BatchOperationExtensions.BatchUpdateJoin)
                    when genericMethod == BatchOperationMethods.BatchUpdateJoinQueryable:
                        query = Expression.Call(
                            BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(genericArguments),
                            methodCallExpression.Arguments[0],
                            methodCallExpression.Arguments[1],
                            methodCallExpression.Arguments[2],
                            methodCallExpression.Arguments[3],
                            methodCallExpression.Arguments[4],
                            methodCallExpression.Arguments[5]);
                        break;

                    case nameof(BatchOperationExtensions.BatchUpdateJoin)
                    when genericMethod == BatchOperationMethods.BatchUpdateJoinReadOnlyList:
                        query = Expression.Call(
                            BatchOperationMethods.BatchUpdateJoin.MakeGenericMethod(genericArguments),
                            methodCallExpression.Arguments[0],
                            Expression.Call(
                                BatchOperationMethods.CreateCommonTable.MakeGenericMethod(genericArguments[0], genericArguments[1]),
                                methodCallExpression.Arguments[0],
                                methodCallExpression.Arguments[1]),
                            methodCallExpression.Arguments[2],
                            methodCallExpression.Arguments[3],
                            methodCallExpression.Arguments[4],
                            methodCallExpression.Arguments[5]);
                        break;
                }

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

        /// <inheritdoc />
        public override Func<QueryContext, TResult> CompileQueryCore<TResult>(
            IDatabase database,
            Expression query,
            IModel model,
            bool async)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.DeclaringType == typeof(BatchOperationExtensions))
            {
                return CompileBulkCore<TResult>(database, methodCallExpression, model, async);
            }
            else
            {
                return base.CompileQueryCore<TResult>(database, query, model, async);
            }
        }

        /// <inheritdoc cref="QueryCompiler.CompileQueryCore{TResult}(IDatabase, Expression, IModel, bool)" />
        protected virtual Func<QueryContext, TResult> CompileBulkCore<TResult>(
            IDatabase database,
            MethodCallExpression methodCallExpression,
            IModel model,
            bool async)
        {
            return base.CompileQueryCore<TResult>(database, methodCallExpression, model, async);
        }
    }
}
