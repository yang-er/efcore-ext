#if EFCORE31
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class BulkParameterValueBasedSelectExpressionOptimizer : ParameterValueBasedSelectExpressionOptimizer
    {
        public BulkParameterValueBasedSelectExpressionOptimizer(
            ISqlExpressionFactory sqlExpressionFactory,
            IParameterNameGeneratorFactory parameterNameGeneratorFactory,
            bool useRelationalNulls)
            : base(sqlExpressionFactory, parameterNameGeneratorFactory, useRelationalNulls)
        {
        }

        public override (SelectExpression selectExpression, bool canCache) Optimize(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues)
        {
            var (optimizedSelectExpression, canCache) = base.Optimize(selectExpression, parametersValues);
            var valuesVisitor = new ValuesExpressionParameterExpandingVisitor(parametersValues);

            optimizedSelectExpression = (SelectExpression)valuesVisitor.Visit(optimizedSelectExpression);
            canCache = canCache && valuesVisitor.CanCache;

            return (optimizedSelectExpression, canCache);
        }
    }
}

#endif
