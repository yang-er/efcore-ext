using Microsoft.EntityFrameworkCore.Metadata;
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
    public class QueryGenerationContext<T>
    {
        const string SELECT = "SELECT";
        const string INSERT = "INSERT";
        const string DELETE = "DELETE";
        const string UPDATE = "UPDATE";

        public QueryGenerationContext(IEnumerable<T> execution, Expression expression)
        {
            InternalExpression = expression;
#if EFCORE50
            var enumerator = (SingleQueryingEnumerable<T>)execution;
#elif EFCORE31
            var enumerator = (QueryingEnumerable<T>)execution;
#endif
            QueryContext = enumerator.Private<RelationalQueryContext>("_relationalQueryContext");
            CommandCache = enumerator.Private<RelationalCommandCache>("_relationalCommandCache");
            var selectExpression = CommandCache.Private<SelectExpression>("_selectExpression");

#if EFCORE50
            SelectExpression = CommandCache
                .Private<RelationalParameterBasedSqlProcessor>("_relationalParameterBasedSqlProcessor")
                .Optimize(selectExpression, QueryContext.ParameterValues, out _);
#elif EFCORE31
            (SelectExpression, _) = CommandCache
                .Private<ParameterValueBasedSelectExpressionOptimizer>("_parameterValueBasedSelectExpressionOptimizer")
                .Optimize(selectExpression, QueryContext.ParameterValues);
#endif
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
