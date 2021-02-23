using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ValuesExpressionExpansionVisitor : SqlExpressionVisitorV2
    {
        private readonly ValuesExpression[] _toReplace, _replacement;

        public ValuesExpressionExpansionVisitor(ValuesExpression[] toReplace, ValuesExpression[] replacement)
        {
            Check.NotNull(toReplace, nameof(toReplace));
            Check.NotNull(replacement, nameof(replacement));
            Check.DebugAssert(toReplace.Length == replacement.Length, "Should be equal length.");

            _toReplace = toReplace;
            _replacement = replacement;
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            for (int i = 0; i < _toReplace.Length; i++)
            {
                if (valuesExpression == _toReplace[i])
                {
                    return _replacement[i];
                }
            }

            return base.VisitValues(valuesExpression);
        }
    }

    public class WorkAroudEFCore31ValuesExpressionExpansionVisitor : SqlExpressionVisitorV2
    {
        private readonly RelationalQueryContext _queryContext;
        private readonly List<(ValuesExpression, ValuesExpression)> _replacement;

        public WorkAroudEFCore31ValuesExpressionExpansionVisitor(RelationalQueryContext relationalQueryContext)
        {
            _queryContext = relationalQueryContext;
            _replacement = new List<(ValuesExpression, ValuesExpression)>();
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            if (valuesExpression.TupleCount == null)
            {
                if (_queryContext.ParameterValues.TryGetValue(valuesExpression.RuntimeParameter, out var _lists)
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
