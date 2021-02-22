using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Collections;
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

        public WorkAroudEFCore31ValuesExpressionExpansionVisitor(RelationalQueryContext relationalQueryContext)
        {
            _queryContext = relationalQueryContext;
        }

        protected override Expression VisitValues(ValuesExpression valuesExpression)
        {
            if (valuesExpression.ParameterValue == null
                && _queryContext.ParameterValues.TryGetValue(valuesExpression.RuntimeParameter.Name, out var _lists)
                && _lists is IList lists)
            {
                return new ValuesExpression(
                    valuesExpression.Alias,
                    lists,
                    valuesExpression.ColumnNames,
                    valuesExpression.AnonymousType,
                    valuesExpression.RuntimeParameter);
            }

            return valuesExpression;
        }
    }
}
