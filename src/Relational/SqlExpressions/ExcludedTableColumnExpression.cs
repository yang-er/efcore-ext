using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// An expression that represents an excluded table column in a SQL tree.
    /// </summary>
    public class ExcludedTableColumnExpression : SqlExpression
    {
        public ExcludedTableColumnExpression(string name, Type type, RelationalTypeMapping typeMapping, bool nullable)
            : base(type, typeMapping)
        {
            Check.NotEmpty(name, nameof(name));

            Name = name;
            IsNullable = nullable;
        }

        /// <summary>
        /// The name of corresponding column.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether this property is nullable.
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// Creates another expression representing this property nullable.
        /// </summary>
        public ExcludedTableColumnExpression MakeNullable()
            => new ExcludedTableColumnExpression(Name, Type.MakeNullable(), TypeMapping, true);

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        /// <inheritdoc />
#if EFCORE50
        protected override void Print(ExpressionPrinter expressionPrinter)
#elif EFCORE31
        public override void Print(ExpressionPrinter expressionPrinter)
#endif
        {
            expressionPrinter.Append("excluded.").Append(Name);
        }
    }
}
