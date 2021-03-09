using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Sqlite.Query
{
    public class SqliteBulkQueryCompiler : BulkQueryCompiler
    {
        public SqliteBulkQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDatabase database,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            IBulkQueryCompilationContextFactory qccFactory,
            IModel model)
            : base(queryContextFactory,
                  compiledQueryCache,
                  compiledQueryCacheKeyGenerator,
                  database,
                  logger,
                  currentContext,
                  evaluatableExpressionFilter,
                  qccFactory,
                  model)
        {
        }

        protected override Func<QueryContext, TResult> CompileBulkCore<TResult>(IDatabase database, Expression query, IModel model, bool async)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.GetGenericMethodDefinition() == BatchOperationMethods.MergeCollapsed)
            {
                throw TranslationFailed(query, "MERGE INTO sentences are not supported in SQLite.");
            }

            return base.CompileBulkCore<TResult>(database, query, model, async);
        }
    }
}
