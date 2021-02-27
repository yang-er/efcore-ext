#if EFCORE31

namespace Microsoft.EntityFrameworkCore.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
    using Microsoft.EntityFrameworkCore.Storage;

    public class XysParameterValueBasedSelectExpressionOptimizer : ParameterValueBasedSelectExpressionOptimizer
    {
        public XysParameterValueBasedSelectExpressionOptimizer(
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
            var valuesVisitor = new ValuesExpressionExpansionVisitor(parametersValues);

            optimizedSelectExpression = (SelectExpression)valuesVisitor.Visit(optimizedSelectExpression);
            canCache = canCache && valuesVisitor.CanCache;

            return (optimizedSelectExpression, canCache);
        }
    }

    public class ValuesExpressionExpansionVisitor : SqlExpressionVisitorV2
    {
        private readonly IReadOnlyDictionary<string, object> _parameterValues;
        private readonly List<(ValuesExpression, ValuesExpression)> _replacement;

        public bool CanCache => _replacement.Count > 0;

        public ValuesExpressionExpansionVisitor(IReadOnlyDictionary<string, object> parameterValues)
        {
            _parameterValues = parameterValues;
            _replacement = new List<(ValuesExpression, ValuesExpression)>();
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            if (valuesExpression.TupleCount == null)
            {
                if (_parameterValues.TryGetValue(valuesExpression.RuntimeParameter, out var _lists)
                    && _lists is IList lists)
                {
                    for (int i = 0; i < _replacement.Count; i++)
                    {
                        if (_replacement[i].Item1 == valuesExpression)
                        {
                            return _replacement[i].Item2;
                        }
                    }

                    var newExpr = new ValuesExpression(valuesExpression, lists.Count);
                    _replacement.Add((valuesExpression, newExpr));
                    return newExpr;
                }
                else
                {
                    throw new InvalidOperationException(
                        "Parameter value corrupted.");
                }
            }

            return valuesExpression;
        }
    }
}

#endif
