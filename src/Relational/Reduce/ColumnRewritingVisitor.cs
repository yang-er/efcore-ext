using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ColumnRewritingExpressionVisitor : ExpressionVisitor
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private TableExpressionBase _from, _to1;
        private IReadOnlyDictionary<string, Expression> _to2;

        public ColumnRewritingExpressionVisitor(
            ISqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        protected virtual Expression VisitColumn(ColumnExpression columnExpression)
        {
            return columnExpression.Table != _from
                ? columnExpression
                : _to2 != null
                ? _to2[columnExpression.Name]
                : _to1 != null
                ? _sqlExpressionFactory.Column(
                    columnExpression.Name,
                    _to1, columnExpression.Type,
                    columnExpression.TypeMapping,
                    columnExpression.IsNullable)
                : throw new ArgumentNullException();
        }

        public IDisposable Setup<TReducer>(TableExpressionBase from, TReducer to) where TReducer : class
        {
            Check.NotNull(from, nameof(from));
            Check.NotNull(to, nameof(to));

            (_from, _to2, _to1) = to switch
            {
                TableExpressionBase table => (from, default(IReadOnlyDictionary<string, Expression>), table),
                IReadOnlyDictionary<string, Expression> projection => (from, projection, default(TableExpressionBase)),
                _ => throw new InvalidOperationException(),
            };

            return new ResettingDisposable(this);
        }

        private void Reset()
        {
            (_from, _to2, _to1) = (null, null, null);
        }

        public override Expression Visit(Expression node)
        {
            if (_from == null) throw new InvalidOperationException("Setup first.");
            return base.Visit(node);
        }

        public SqlExpression Visit(SqlExpression node)
        {
            if (_from == null) throw new InvalidOperationException("Setup first.");
            return (SqlExpression)base.Visit(node);
        }

        public void Visit<TExpression>(List<TExpression> expressions) where TExpression : Expression
        {
            if (_from == null) throw new InvalidOperationException("Setup first.");
            for (int i = 0; i < expressions.Count; i++)
            {
                expressions[i] = (TExpression)Visit(expressions[i]);
            }
        }

        public void Visit<TKey, TExpression>(IDictionary<TKey, TExpression> expressions)
            where TKey : notnull
            where TExpression : Expression
        {
            if (_from == null) throw new InvalidOperationException("Setup first.");
            foreach (var i in expressions.Keys.ToList())
            {
                expressions[i] = (TExpression)Visit(expressions[i]);
            }
        }

        private readonly struct ResettingDisposable : IDisposable
        {
            private readonly ColumnRewritingExpressionVisitor _visitor;

            public ResettingDisposable(ColumnRewritingExpressionVisitor visitor)
            {
                _visitor = visitor;
            }

            public void Dispose()
            {
                _visitor.Reset();
            }
        }

        protected override Expression VisitExtension(Expression node)
        {
            return node switch
            {
                ColumnExpression columnExpression => VisitColumn(columnExpression),
                _ => base.VisitExtension(node)
            };
        }
    }
}
