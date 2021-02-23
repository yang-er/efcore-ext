using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class TranslationStrategy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QueryRewritingContext Go<T>(DbContext context, IQueryable<T> query)
        {
            return StepIn(context, query);
            //return System(query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QueryRewritingContext System<T>(IQueryable<T> query)
        {
            var enumerator = query.Provider.Execute<IEnumerable<T>>(query.Expression);
            return QueryRewritingContext.Create(enumerator, query.Expression);
        }

        private static readonly MethodInfo _queryContextAddParameterMethodInfo
            = typeof(QueryContext)
                .GetTypeInfo()
                .GetDeclaredMethod(nameof(QueryContext.AddParameter));

        public static QueryRewritingContext StepIn<T>(DbContext context, IQueryable<T> queryable)
        {
            var query = queryable.Expression;
            var evaluatableExpressionFilter = context.GetService<IEvaluatableExpressionFilter>();
            var queryCompilationContext = context.GetService<IQueryCompilationContextFactory>().Create(false);
            var logger = queryCompilationContext.Logger;

            // step 1: extract all parameters to check whether this queryable is compiled before.
            var queryContext = context.GetService<IQueryContextFactory>().Create();
            query = ExtractParameters(evaluatableExpressionFilter, query, queryContext, logger, context.GetType(), context.Model);

            // step 2: preprocess
            query = context.GetService<IQueryTranslationPreprocessorFactory>()
                .Create(queryCompilationContext).Process(query);

            // step 3: method translating to be a ShapedQueryExpression
            query = context.GetService<IQueryableMethodTranslatingExpressionVisitorFactory>()
                .Create(queryCompilationContext).Visit(query);

            // step 4: postprocess
            query = context.GetService<IQueryTranslationPostprocessorFactory>()
                .Create(queryCompilationContext).Process(query);

            // step 5: ShapedQueryExpression -> [ new QueryableEnumerable(..) ]
            query = context.GetService<IShapedQueryCompilingExpressionVisitorFactory>()
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
            return QueryRewritingContext.Create(enumerator, queryable.Expression);
        }

#if EFCORE31
        private static QueryableMethodTranslatingExpressionVisitor Create(
            this IQueryableMethodTranslatingExpressionVisitorFactory factory,
            QueryCompilationContext queryCompilationContext)
        {
            return factory.Create(queryCompilationContext.Model);
        }
#endif

        public static Expression ExtractParameters(
            IEvaluatableExpressionFilter evaluatableExpressionFilter,
            Expression query,
            IParameterValues parameterValues,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            Type contextType,
            IModel model,
            bool parameterize = true,
            bool generateContextAccessors = false)
        {
            var visitor = new ParameterExtractingExpressionVisitorV2(
                evaluatableExpressionFilter,
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
