using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc cref="IQueryCompilationContextFactory" />
    /// <remarks>
    ///     This factory type is designed to replace several services in <see cref="QueryCompilationContextDependencies"/>.
    ///     It also exposes the direct <see cref="CreateQueryExecutor"/> and <see cref="CreateQueryExecutor{TResult}"/> functions.
    /// </remarks>
    public interface IBulkQueryCompilationContextFactory : IQueryCompilationContextFactory
    {
        /// <summary>
        ///     Creates the query executor func which gives results for this query.
        /// </summary>
        /// <typeparam name="TResult"> The result type of this query. </typeparam>
        /// <param name="async"> Specifies whether the query is async. </param>
        /// <param name="query"> The query to generate executor for. </param>
        /// <returns> Returns <see cref="Func{QueryContext, TResult}" /> which can be invoked to get results of this query. </returns>
        Func<QueryContext, TResult> CreateQueryExecutor<TResult>(bool async, Expression query);

        /// <summary>
        ///     Creates the query executor func which gives results for this query.
        /// </summary>
        /// <param name="async"> Specifies whether the query is async. </param>
        /// <param name="query"> The query to generate executor for. </param>
        /// <param name="resultType"> The result type of this query. </param>
        /// <returns> Returns <see cref="Func{QueryContext, TResult}" /> which can be invoked to get results of this query. </returns>
        Delegate CreateQueryExecutor(bool async, Expression query, Type resultType);
    }

    /// <inheritdoc cref="QueryCompilationContextFactory" />
    public class BulkQueryCompilationContextFactory :
        IBulkQueryCompilationContextFactory,
        IServiceAnnotation<IQueryCompilationContextFactory, QueryCompilationContextFactory>
    {
        /// <summary>
        ///     The <see cref="MethodInfo"/> for <see cref="CreateQueryExecutor{TResult}(bool, Expression)"/>.
        /// </summary>
        private static MethodInfo CreateQueryExecutorMethod { get; }
            = typeof(QueryCompilationContext).GetMethod(nameof(QueryCompilationContext.CreateQueryExecutor));

        /// <summary>
        ///     Parameter object containing dependencies for this service.
        /// </summary>
        protected virtual QueryCompilationContextDependencies Dependencies { get; }

        /// <summary>
        ///     <para>
        ///         This is an internal API that supports the Entity Framework Core extension and not subject to
        ///         the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///         any release. You should only use it directly in your code with extreme caution and knowing that
        ///         doing so can result in application failures when updating to a new Entity Framework Core release.
        ///     </para>
        /// </summary>
        public BulkQueryCompilationContextFactory(
            QueryCompilationContextDependencies dependencies,
            IBulkQueryTranslationPreprocessorFactory queryTranslationPreprocessorFactory,
            IBulkQueryableMethodTranslatingExpressionVisitorFactory queryableMethodTranslatingExpressionVisitorFactory,
            IBulkQueryTranslationPostprocessorFactory queryTranslationPostprocessorFactory,
            IBulkShapedQueryCompilingExpressionVisitorFactory shapedQueryCompilingExpressionVisitorFactory)
        {
            Dependencies = dependencies
                .With(queryTranslationPreprocessorFactory)
                .With(queryableMethodTranslatingExpressionVisitorFactory)
                .With(queryTranslationPostprocessorFactory)
                .With(shapedQueryCompilingExpressionVisitorFactory);
        }

        /// <inheritdoc />
        public virtual QueryCompilationContext Create(bool async)
        {
            return new QueryCompilationContext(Dependencies, async);
        }

        /// <summary>
        ///     Creates the query executor func which gives results for this query.
        /// </summary>
        /// <typeparam name="TResult"> The result type of this query. </typeparam>
        /// <param name="async"> Specifies whether the query is async. </param>
        /// <param name="query"> The query to generate executor for. </param>
        /// <returns> Returns <see cref="Func{QueryContext, TResult}" /> which can be invoked to get results of this query. </returns>
        public Func<QueryContext, TResult> CreateQueryExecutor<TResult>(bool async, Expression query)
        {
            return Create(async).CreateQueryExecutor<TResult>(query);
        }

        /// <summary>
        ///     Creates the query executor func which gives results for this query.
        /// </summary>
        /// <param name="async"> Specifies whether the query is async. </param>
        /// <param name="query"> The query to generate executor for. </param>
        /// <param name="resultType"> The result type of this query. </param>
        /// <returns> Returns <see cref="Func{QueryContext, TResult}" /> which can be invoked to get results of this query. </returns>
        public Delegate CreateQueryExecutor(bool async, Expression query, Type resultType)
        {
            var queryCompilationContext = Create(async);

            try
            {
                return (Delegate)CreateQueryExecutorMethod
                    .MakeGenericMethod(resultType)
                    .Invoke(queryCompilationContext, new[] { query });
            }
            catch (TargetInvocationException ex)
            {
                throw ex?.InnerException ?? ex;
            }
        }
    }
}
