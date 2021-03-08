using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

        private Expression RewriteUpsertCollapsed(IModel model, MethodCallExpression query)
        {
            var insertExpression = query.Arguments[2].UnwrapLambdaFromQuote();
            var updateExpression = query.Arguments[3].UnwrapLambdaFromQuote();

            var newUpdate = UpsertToMergeRewriter.Process(insertExpression, updateExpression, out var errlogs);
            if (newUpdate == null || errlogs.Count > 0)
            {
                throw TranslationFailed(query, errlogs.ToArray());
            }

            var types = query.Method.GetGenericArguments();
            Type targetType = types[0], sourceType = types[1];
            if (insertExpression.Body is not MemberInitExpression keyBody ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
            {
                throw TranslationFailed(query, "Insert expression must be empty constructor and contain member initialization.");
            }

            var entityType = model.FindEntityType(targetType);
            if (!entityType.TryGuessKey(keyBody.Bindings, out var key))
            {
                throw TranslationFailed(query, $"No corresponding primary key or alternative key found in this expression for {entityType}.");
            }

            GenericUtility.CreateJoinKey(
                Expression.Parameter(targetType, "t"),
                key,
                insertExpression.Parameters[0],
                keyBody.Bindings,
                out var joinKeyType,
                out var targetKeySelector,
                out var sourceKeySelector);

            return Expression.Call(
                BatchOperationMethods.MergeCollapsed.MakeGenericMethod(targetType, sourceType, joinKeyType),
                query.Arguments[0],
                query.Arguments[1],
                Expression.Quote(targetKeySelector),
                Expression.Quote(sourceKeySelector),
                Expression.Quote(newUpdate),
                Expression.Quote(insertExpression),
                Expression.Constant(false));
        }

        private Expression RewriteUpsertOneCollapsed(IModel model, MethodCallExpression query)
        {
            var insertExpression = query.Arguments[1].UnwrapLambdaFromQuote();
            var updateExpression = query.Arguments[2].UnwrapLambdaFromQuote();

            var targetType = query.Method.GetGenericArguments().Single();
            if (insertExpression.Body is not MemberInitExpression keyBody ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
            {
                throw TranslationFailed(query, "Insert expression must be empty constructor and contain member initialization.");
            }

            var entityType = model.FindEntityType(targetType);
            if (!entityType.TryGuessKey(keyBody.Bindings, out var key))
            {
                throw TranslationFailed(query, $"No corresponding primary key or alternative key found in this expression for {entityType}.");
            }

            var targetParam = Expression.Parameter(targetType, "t");

            GenericUtility.CreateJoinKey(
                targetParam,
                key,
                targetParam,
                keyBody.Bindings,
                out var joinKeyType,
                out var targetKeySelector,
                out var sourceKeyUnfinished);

            var sourceParam = Expression.Parameter(joinKeyType, "s");
            var unfinishedBody = (NewExpression)sourceKeyUnfinished.Body;
            var fields = joinKeyType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var changedFields = fields.Select(f => Expression.Field(sourceParam, f)).ToArray();
            var replacer = new ReplacingExpressionVisitor(unfinishedBody.Arguments.ToArray(), changedFields);

            return Expression.Call(
                BatchOperationMethods.MergeCollapsed.MakeGenericMethod(targetType, joinKeyType, joinKeyType),
                query.Arguments[0],
                Expression.Call(
                    BatchOperationMethods.CreateSingleTuple.MakeGenericMethod(targetType, joinKeyType),
                    query.Arguments[0],
                    Expression.Quote(
                        Expression.Lambda(
                            Expression.New(
                                unfinishedBody.Constructor,
                                unfinishedBody.Arguments,
                                fields)))),
                Expression.Quote(targetKeySelector),
                Expression.Quote(
                    Expression.Lambda(
                        Expression.New(
                            unfinishedBody.Constructor,
                            changedFields),
                        sourceParam)),
                Expression.Quote(
                    Expression.Lambda(
                        replacer.Visit(updateExpression.Body),
                        updateExpression.Parameters[0],
                        sourceParam)),
                Expression.Quote(
                    Expression.Lambda(
                        replacer.Visit(insertExpression.Body),
                        sourceParam)),
                Expression.Constant(false));
        }

        protected override Func<QueryContext, TResult> CompileBulkCore<TResult>(IDatabase database, Expression query, IModel model, bool async)
        {
            if (query is MethodCallExpression methodCallExpression
                && methodCallExpression.Method.Name == nameof(BatchOperationExtensions.Upsert))
            {
                var genericMethod = methodCallExpression.Method.GetGenericMethodDefinition();
                if (genericMethod == BatchOperationMethods.UpsertCollapsed)
                {
                    query = RewriteUpsertCollapsed(model, methodCallExpression);
                }
                else if (genericMethod == BatchOperationMethods.UpsertOneCollapsed)
                {
                    query = RewriteUpsertOneCollapsed(model, methodCallExpression);
                }
            }

            return base.CompileBulkCore<TResult>(database, query, model, async);
        }

        Exception TranslationFailed(Expression query, params string[] errlogs)
            => new InvalidOperationException(
                string.Concat(
                    CoreStrings.TranslationFailed(query.Print()),
                    Environment.NewLine,
                    "Details:",
                    string.Join(Environment.NewLine, errlogs)));
    }
}
