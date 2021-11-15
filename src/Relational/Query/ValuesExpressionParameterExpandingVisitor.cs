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
        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public bool CanCache => _replacement.Count == 0;

        public ValuesExpressionParameterExpandingVisitor(
            ISqlExpressionFactory sqlExpressionFactory,
            IReadOnlyDictionary<string, object> parameterValues)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _parameterValues = parameterValues;
            _replacement = new List<(ValuesExpression, ValuesExpression)>();
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
#if EFCORE60
            return columnExpression;

            // TODO: clear out these code
            var result =
                columnExpression.Table is InnerJoinExpression innerJoin
                    && innerJoin.Table is ValuesExpression valuesExpression
                ? (TableExpressionBase)Visit(valuesExpression)
                : (TableExpressionBase)Visit(columnExpression.Table);
#else
            var result = (TableExpressionBase)Visit(columnExpression.Table);
#endif
            return result != columnExpression.Table
                ? _sqlExpressionFactory.Column(columnExpression, result)
                : columnExpression;
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
