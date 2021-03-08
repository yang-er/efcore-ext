using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ValuesExpressionParameterExpandingVisitor : SqlExpressionVisitorV2
    {
        private readonly IReadOnlyDictionary<string, object> _parameterValues;
        private readonly List<(ValuesExpression, ValuesExpression)> _replacement;

        public bool CanCache => _replacement.Count > 0;

        public ValuesExpressionParameterExpandingVisitor(IReadOnlyDictionary<string, object> parameterValues)
        {
            _parameterValues = parameterValues;
            _replacement = new List<(ValuesExpression, ValuesExpression)>();
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            if (valuesExpression.ImmediateValues == null
                && valuesExpression.RuntimeParameter != null
                && valuesExpression.TupleCount == null)
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
