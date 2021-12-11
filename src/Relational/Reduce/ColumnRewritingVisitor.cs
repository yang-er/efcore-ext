using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

#if EFCORE31 || EFCORE50
using TableReferenceExpression = Microsoft.EntityFrameworkCore.Query.SqlExpressions.TableExpressionBase;
#elif EFCORE60
using TableReferenceExpression = Microsoft.EntityFrameworkCore.Query.BulkSqlExpressionFactoryExtensions.TableReferenceExpression;
#endif

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ColumnRewritingExpressionVisitor : ExpressionVisitor
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private TableReferenceExpression _from, _to1;
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
                ? _sqlExpressionFactory.Column(columnExpression, _to1)
                : throw new ArgumentNullException();
        }

        public IDisposable Setup(TableReferenceExpression from, TableReferenceExpression to)
        {
            _from = Check.NotNull(from, nameof(from));
            _to1 = Check.NotNull(to, nameof(to));
            _to2 = null;

            return new ResettingDisposable(this);
        }

#if EFCORE60
        [Obsolete]
        public IDisposable Setup(TableExpressionBase from, TableExpressionBase to)
        {
            throw new NotImplementedException();
        }
#endif

        public IDisposable Setup(TableReferenceExpression from, IReadOnlyDictionary<string, Expression> to)
        {
            _from = Check.NotNull(from, nameof(from));
            _to2 = Check.NotNull(to, nameof(to));
            _to1 = null;

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
