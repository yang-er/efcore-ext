using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
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

        public (IRelationalCommand, List<object>) Generate(Expression expression)
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

            var @params = new List<object>();
            var queryContext = QueryContext;

            void AddParameter(IRelationalParameter parInfo)
            {
                if (parInfo is TypeMappedRelationalParameter parInfo1)
                    @params.Add(generator.CreateParameter(queryContext, parInfo1));
                else if (parInfo is CompositeRelationalParameter compo)
                    foreach (var smallPar in compo.RelationalParameters)
                        AddParameter(smallPar);
                else
                    throw new NotSupportedException(parInfo.GetType().Name + " not supported yet.");
            }

            foreach (var para in command.Parameters)
                AddParameter(para);
            return (command, @params);
        }

        public IEnhancedQuerySqlGenerator CreateGenerator()
        {
            return CommandCache
                .Private<IQuerySqlGeneratorFactory>("_querySqlGeneratorFactory")
                .Create() as IEnhancedQuerySqlGenerator;
        }
    }
}
