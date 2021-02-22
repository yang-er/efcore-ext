using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{

#if EFCORE50

    public class XysSqlNullabilityProcessor : NpgsqlSqlNullabilityProcessor
    {
        private readonly HashSet<ValuesExpression> _valuesExpressions;

        public XysSqlNullabilityProcessor(
            RelationalParameterBasedSqlProcessorDependencies dependencies,
            bool useRelationalNulls,
            HashSet<ValuesExpression> valuesExpressions)
            : base(dependencies, useRelationalNulls)
        {
            _valuesExpressions = valuesExpressions;
        }

        protected override TableExpressionBase Visit(TableExpressionBase tableExpressionBase)
        {
            if (tableExpressionBase is ValuesExpression values)
            {
                DoNotCache();
                _valuesExpressions.Add(values);
                return values;
            }

            return base.Visit(tableExpressionBase);
        }
    }

    public class XysParameterBasedSqlProcessor : NpgsqlParameterBasedSqlProcessor
    {
        private readonly HashSet<ValuesExpression> _valuesTables;

        public XysParameterBasedSqlProcessor(
            RelationalParameterBasedSqlProcessorDependencies dependencies,
            bool useRelationalNulls)
            : base(dependencies, useRelationalNulls)
        {
            _valuesTables = new HashSet<ValuesExpression>();
        }

        protected override SelectExpression ProcessSqlNullability(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues,
            out bool canCache)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(parametersValues, nameof(parametersValues));

            return new XysSqlNullabilityProcessor(Dependencies, UseRelationalNulls, _valuesTables)
                .Process(selectExpression, parametersValues, out canCache);
        }

        protected virtual SelectExpression ProcessValuesExpansion(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues)
        {
            if (_valuesTables.Count == 0) return selectExpression;
            var toReplace = _valuesTables.ToArray();
            var replacement = new ValuesExpression[toReplace.Length];

            for (int i = 0; i < toReplace.Length; i++)
            {
                var target = toReplace[i];
                if (parametersValues.TryGetValue(target.RuntimeParameter.Name, out var param)
                    && param is IList lists)
                {
                    replacement[i] = new ValuesExpression(
                        target.Alias,
                        lists,
                        target.ColumnNames,
                        target.AnonymousType,
                        target.RuntimeParameter);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Parameter value corrupted.");
                }
            }

            return (SelectExpression)new ValuesExpressionExpansionVisitor(toReplace, replacement)
                .Visit(selectExpression);
        }

        public override SelectExpression Optimize(
            SelectExpression selectExpression,
            IReadOnlyDictionary<string, object> parametersValues,
            out bool canCache)
        {
            selectExpression = base.Optimize(selectExpression, parametersValues, out canCache);
            selectExpression = ProcessValuesExpansion(selectExpression, parametersValues);
            return selectExpression;
        }
    }

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

    public class XysParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
    {
        private readonly RelationalParameterBasedSqlProcessorDependencies _dependencies;

        public XysParameterBasedSqlProcessorFactory(
            RelationalParameterBasedSqlProcessorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));

            _dependencies = dependencies;
        }

        public virtual RelationalParameterBasedSqlProcessor Create(bool useRelationalNulls)
            => new XysParameterBasedSqlProcessor(_dependencies, useRelationalNulls);
    }

#endif

}
