using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class QueryRewritingContext
    {
        private QueryRewritingContext(
            Expression originalQueryExpression,
            RelationalQueryContext queryContext,
            RelationalCommandCache commandCache,
            SelectExpression selectExpression)
        {
            InternalExpression = originalQueryExpression;
            QueryContext = queryContext;
            CommandCache = commandCache;
            SelectExpression = selectExpression;
        }

        public static QueryRewritingContext Create<T>(IEnumerable<T> execution, Expression expression)
        {
#if EFCORE50
            var enumerator = (SingleQueryingEnumerable<T>)execution;
#elif EFCORE31
            var enumerator = (QueryingEnumerable<T>)execution;
#endif
            var queryContext = enumerator.Private<RelationalQueryContext>("_relationalQueryContext");
            var commandCache = enumerator.Private<RelationalCommandCache>("_relationalCommandCache");
            var selectExpression = commandCache.Private<SelectExpression>("_selectExpression");

#if EFCORE50
            selectExpression = commandCache
                .Private<RelationalParameterBasedSqlProcessor>("_relationalParameterBasedSqlProcessor")
                .Optimize(selectExpression, queryContext.ParameterValues, out _);
#elif EFCORE31
            (selectExpression, _) = commandCache
                .Private<ParameterValueBasedSelectExpressionOptimizer>("_parameterValueBasedSelectExpressionOptimizer")
                .Optimize(selectExpression, queryContext.ParameterValues);
#endif

            return new QueryRewritingContext(expression, queryContext, commandCache, selectExpression);
        }

        public Expression InternalExpression { get; }

        public RelationalCommandCache CommandCache { get; }

        public RelationalQueryContext QueryContext { get; }

        public SelectExpression SelectExpression { get; }

        public RelationalNonQueryExecutor Generate(Expression expression)
        {
            var generator = CreateGenerator();
            var command = expression switch
            {
                SelectExpression selectExpression => generator.GetCommand(selectExpression),
                UpdateExpression updateExpression => generator.GetCommand(updateExpression),
                UpsertExpression upsertExpression => generator.GetCommand(upsertExpression),
                MergeExpression mergeExpression => generator.GetCommand(mergeExpression),
                DeleteExpression deleteExpression => generator.GetCommand(deleteExpression),
                SelectIntoExpression selectIntoExpression => generator.GetCommand(selectIntoExpression),
                _ => throw new ArgumentNullException(nameof(expression)),
            };

            return new RelationalNonQueryExecutor(QueryContext, command);
        }

        public IEnhancedQuerySqlGenerator CreateGenerator()
        {
            return CommandCache
                .Private<IQuerySqlGeneratorFactory>("_querySqlGeneratorFactory")
                .Create() as IEnhancedQuerySqlGenerator;
        }
    }
}
