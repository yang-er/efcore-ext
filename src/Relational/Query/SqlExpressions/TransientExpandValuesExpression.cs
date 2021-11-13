using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public class TransientExpandValuesExpression : SqlExpressionVisitorV2
    {
        private readonly ValuesExpression _valuesExpression;
        private readonly SqlExpression[] _bypassSource;
        private readonly SqlExpression[] _exposedExpression;
        private readonly Dictionary<string, int> _idxs;

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            return columnExpression.Table != _valuesExpression
                ? base.VisitColumn(columnExpression)
                : new BypassSqlExpression(
                    _bypassSource,
                    _idxs[columnExpression.Name],
                    columnExpression.Type,
                    columnExpression.TypeMapping);
        }

        public static void Process(
            ValuesExpression valuesExpression,
            IReadOnlyList<ProjectionExpression> projections,
            QuerySqlGenerator visitor,
            IRelationalCommandBuilder sql,
            ISqlGenerationHelper helper)
        {
            var source = new TransientExpandValuesExpression(valuesExpression, projections.Select(a => a.Expression));
            sql.Append("VALUES ");

            if (valuesExpression.TupleCount.HasValue)
            {
                var paramName = helper.GenerateParameterNamePlaceholder(valuesExpression.RuntimeParameter);

                sql.AddParameter(
                    new ValuesRelationalParameter(
                        valuesExpression.AnonymousType,
                        helper.GenerateParameterName(valuesExpression.RuntimeParameter),
                        valuesExpression.RuntimeParameter));

                for (int i = 0; i < valuesExpression.TupleCount.Value; i++)
                {
                    if (i > 0) sql.AppendLine(",");

                    for (int j = 0; j < valuesExpression.ColumnNames.Count; j++)
                    {
                        source._bypassSource[j] = new SqlFragmentExpression($"{paramName}_{i}_{j}");
                    }

                    sql.Append("(").GenerateList(source._exposedExpression, e => visitor.Visit(e)).Append(")");
                }
            }
            else if (valuesExpression.ImmediateValues != null)
            {
                for (int i = 0; i < valuesExpression.ImmediateValues.Count; i++)
                {
                    if (i > 0) sql.AppendLine(",");

                    for (int j = 0; j < valuesExpression.ColumnNames.Count; j++)
                    {
                        source._bypassSource[j] = valuesExpression.ImmediateValues[i][j];
                    }

                    sql.Append("(").GenerateList(source._exposedExpression, e => visitor.Visit(e)).Append(")");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "This instance of values expression is not concrete.");
            }
        }

        private TransientExpandValuesExpression(
            ValuesExpression values,
            IEnumerable<SqlExpression> projections)
        {
            _valuesExpression = values;
            _bypassSource = new SqlExpression[_valuesExpression.ColumnNames.Count];
            _idxs = values.ColumnNames.Select((a, i) => (a, i)).ToDictionary(a => a.a, a => a.i);
            _exposedExpression = projections.Select(e => Visit(e)).Cast<SqlExpression>().ToArray();
        }

        private class BypassSqlExpression : SqlExpression
        {
            private readonly SqlExpression[] _bypassSource;
            private readonly int _bypassIndex;

            public BypassSqlExpression(
                SqlExpression[] bypassSource,
                int bypassIndex,
                Type type,
                RelationalTypeMapping typeMapping)
                : base(type, typeMapping)
            {
                _bypassSource = bypassSource;
                _bypassIndex = bypassIndex;
            }

            protected override Expression VisitChildren(ExpressionVisitor visitor)
            {
                visitor.Visit(_bypassSource[_bypassIndex]);
                return this;
            }

#if EFCORE50 || EFCORE60
            protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
            public override void Print(ExpressionPrinter expressionPrinter)
#endif
            {
            }
        }
    }
}
