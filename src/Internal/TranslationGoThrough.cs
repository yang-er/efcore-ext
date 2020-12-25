using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class TranslationStrategy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QueryGenerationContext<T> Go<T>(IQueryable<T> query)
        {
            return StepIn(query);
            return System(query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QueryGenerationContext<T> System<T>(IQueryable<T> query)
        {
            var enumerator = query.Provider.Execute<IEnumerable<T>>(query.Expression);
            return new QueryGenerationContext<T>(enumerator, query.Expression);
        }

        private static readonly MethodInfo _queryContextAddParameterMethodInfo
            = typeof(QueryContext)
                .GetTypeInfo()
                .GetDeclaredMethod(nameof(QueryContext.AddParameter));

        public static QueryGenerationContext<T> StepIn<T>(IQueryable<T> queryable)
        {
            var context = queryable.GetDbContext();
            var query = queryable.Expression;
            var queryCompiler = queryable.Provider.Private<QueryCompiler>("_queryCompiler");
            var queryCompilationContext = context.GetService<IQueryCompilationContextFactory>().Create(false);
            var logger = queryCompilationContext.Logger;

            // step 1: extract all parameters to check whether this queryable is compiled before.
            var queryContext = queryCompiler.Private<IQueryContextFactory>("_queryContextFactory").Create();
            query = ExtractParameters(queryCompiler, query, queryContext, logger, context.GetType(), context.Model);

            // step 2: preprocess
            query = queryCompilationContext
                .Private<IQueryTranslationPreprocessorFactory>("_queryTranslationPreprocessorFactory")
                .Create(queryCompilationContext).Process(query);

            // step 3: method translating to be a ShapedQueryExpression
            query = queryCompilationContext
                .Private<IQueryableMethodTranslatingExpressionVisitorFactory>("_queryableMethodTranslatingExpressionVisitorFactory")
                .Create(context.Model).Visit(query);

            // step 4: postprocess
            query = queryCompilationContext
                .Private<IQueryTranslationPostprocessorFactory>("_queryTranslationPostprocessorFactory")
                .Create(queryCompilationContext).Process(query);

            // step 5: ShapedQueryExpression -> [ new QueryableEnumerable(..) ]
            query = queryCompilationContext
                .Private<IShapedQueryCompilingExpressionVisitorFactory>("_shapedQueryCompilingExpressionVisitorFactory")
                .Create(queryCompilationContext).Visit(query);

            // step 6: append AddParameter sequences before [ new QueryableEnumerable ]
            var runtimeParameters = queryCompilationContext.Private<Dictionary<string, LambdaExpression>>("_runtimeParameters");
            if (runtimeParameters != null)
                query = Expression.Block(runtimeParameters
                    .Select(kv => Expression.Call(
                        QueryCompilationContext.QueryContextParameter,
                        _queryContextAddParameterMethodInfo,
                        Expression.Constant(kv.Key),
                        Expression.Convert(Expression.Invoke(kv.Value, QueryCompilationContext.QueryContextParameter),
                        typeof(object))))
                    .Append(query));

            // step 7: become a lambda expression to be compiled
            var queryExecutorExpression = Expression.Lambda<Func<QueryContext, IEnumerable<T>>>(
                query, QueryCompilationContext.QueryContextParameter);
            Func<QueryContext, IEnumerable<T>> compiled = null;

            try
            {
                compiled = queryExecutorExpression.Compile();
            }
            finally
            {
                logger.QueryExecutionPlanned(new ExpressionPrinter(), queryExecutorExpression);
            }

            // step 8: begin the execution
            var enumerator = compiled(queryContext);
            return new QueryGenerationContext<T>(enumerator, queryable.Expression);
        }

        public static Expression ExtractParameters(
            [NotNull] QueryCompiler queryCompiler,
            [NotNull] Expression query,
            [NotNull] IParameterValues parameterValues,
            [NotNull] IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            [NotNull] Type contextType,
            [NotNull] IModel model,
            bool parameterize = true,
            bool generateContextAccessors = false)
        {
            var visitor = new ParameterExtractingExpressionVisitor2(
                queryCompiler.Private<IEvaluatableExpressionFilter>("_evaluatableExpressionFilter"),
                parameterValues,
                contextType,
                model,
                logger,
                parameterize,
                generateContextAccessors);

            return visitor.ExtractParameters(query);
        }
    }
}
