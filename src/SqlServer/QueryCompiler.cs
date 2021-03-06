using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query
{
    public class SqlServerBulkQueryCompiler : BulkQueryCompiler
    {
        public SqlServerBulkQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDatabase database,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            IBulkQueryCompilationContextFactory bulkQueryCompilationContextFactory,
            IModel model)
            : base(queryContextFactory,
                  compiledQueryCache,
                  compiledQueryCacheKeyGenerator,
                  database,
                  logger,
                  currentContext,
                  evaluatableExpressionFilter,
                  bulkQueryCompilationContextFactory,
                  model)
        {
        }

        protected override Func<QueryContext, TResult> CompileBulkCore<TResult>(IDatabase database, Expression query, IModel model, bool async)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.Name == nameof(BatchOperationExtensions.Upsert)
                && methodCallExpression.Method.GetGenericMethodDefinition() == BatchOperationMethods.UpsertCollapsed)
            {
                var insertExpression = methodCallExpression.Arguments[2].UnwrapLambdaFromQuote();
                var updateExpression = methodCallExpression.Arguments[3].UnwrapLambdaFromQuote();

                var newUpdate = UpsertToMergeRewriter.Process(insertExpression, updateExpression, out var errlogs);
                if (newUpdate == null || errlogs.Count > 0)
                {
                    throw TranslationFailed(errlogs.ToArray());
                }

                var types = methodCallExpression.Method.GetGenericArguments();
                Type targetType = types[0], sourceType = types[1];
                if (insertExpression.Body is not MemberInitExpression keyBody ||
                    keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                {
                    throw TranslationFailed("Insert expression must be empty constructor and contain member initialization.");
                }

                var entityType = model.FindEntityType(targetType);
                if (!entityType.TryGuessKey(keyBody.Bindings, out var key))
                {
                    throw new NotSupportedException($"No corresponding primary key or alternative key found in this expression for {entityType}.");
                }

                GenericUtility.CreateJoinKey(
                    Expression.Parameter(targetType, "t"),
                    key,
                    insertExpression.Parameters[0],
                    keyBody.Bindings,
                    out var joinKeyType,
                    out var targetKeySelector,
                    out var sourceKeySelector);

                query = Expression.Call(
                    BatchOperationMethods.MergeCollapsed.MakeGenericMethod(targetType, sourceType, joinKeyType),
                    methodCallExpression.Arguments[0],
                    methodCallExpression.Arguments[1],
                    targetKeySelector,
                    sourceKeySelector,
                    Expression.Quote(newUpdate),
                    Expression.Quote(insertExpression),
                    Expression.Constant(false));
            }

            return base.CompileBulkCore<TResult>(database, query, model, async);

            Exception TranslationFailed(params string[] errlogs)
                => new InvalidOperationException(
                    string.Concat(
                        CoreStrings.TranslationFailed(query.Print()),
                        Environment.NewLine,
                        "Details:",
                        string.Join(Environment.NewLine, errlogs)));
        }
    }
}
