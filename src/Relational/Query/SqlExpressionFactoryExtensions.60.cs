using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public static partial class BulkSqlExpressionFactoryExtensions
    {
        internal static Expression AddCrossJoinForMerge(
            this SelectExpression outerSelectExpression,
            ShapedQueryExpression innerSource,
            Expression outerShaper)
            => outerSelectExpression.AddCrossJoin(innerSource, outerShaper);

        private static ColumnExpression ColumnExpressionConstructor(string name, TableReferenceExpression table, Type type, RelationalTypeMapping typeMapping, bool nullable)
            => ConcreteColumnExpressionConstructor(name, table, type, typeMapping, nullable);

        private static ColumnExpression ColumnExpressionConstructor(string name, TableExpressionBase table, Type type, RelationalTypeMapping typeMapping, bool nullable)
            => new AnyCastColumnExpression(name, table, type, typeMapping, nullable);

        public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> SqlParameterExpressionConstructor
            = typeof(SqlParameterExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression>;

        public static readonly Func<SqlExpression, string, ProjectionExpression> ProjectionExpressionConstructor
            = typeof(ProjectionExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<SqlExpression, string, ProjectionExpression>;

        public static readonly Func<ITableBase, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ITableBase, TableExpression>;

        public static readonly Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression> SelectExpressionConstructor
            = new Func<Expression<Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>>>(delegate
            {
                var constructor = typeof(SelectExpression)
                    .GetConstructors(GeneralBindingFlags)
                    .Single(c => c.GetParameters().Length == 6);
                var arguments = constructor.GetParameters()
                    .Select((p, i) => i == 3
                        ? Expression.New(p.ParameterType.GetConstructor(Type.EmptyTypes))
                        : Expression.Parameter(p.ParameterType) as Expression)
                    .ToArray();
                var @params = arguments.OfType<ParameterExpression>();
                return Expression.Lambda<Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>>(
                    Expression.New(constructor, arguments),
                    @params);
            }).Invoke().Compile();

        public static Func<SelectExpression, IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>>> AccessEntityProjectionCache
            => throw new NotSupportedException();

        public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_projectionMapping")
                .Compile<Func<SelectExpression, IDictionary<ProjectionMember, Expression>>>();

        private static readonly Func<SelectExpression, IEnumerable<Expression>> AccessTableReferenceExpression
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_tableReferences")
                .Compile<Func<SelectExpression, IEnumerable<Expression>>>();

        private static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Predicate"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        private static readonly Action<SelectExpression, SqlExpression> ApplyHaving
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Having"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        private static readonly Action<TableExpressionBase, string> ApplyAlias
            = new Func<Expression<Action<TableExpressionBase, string>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
                var para2 = Expression.Parameter(typeof(string), "alias");
                var body = Expression.Assign(Expression.Property(para1, "Alias"), para2);
                return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2);
            })
            .Invoke().Compile();

        private static readonly Action<SelectExpression, SelectExpression> ApplyCopyIdentifiersFrom
            = new Func<Expression<Action<SelectExpression, SelectExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "left");
                var para2 = Expression.Parameter(typeof(SelectExpression), "right");
                var _identifier = typeof(SelectExpression).GetField("_identifier", GeneralBindingFlags);
                var left = Expression.Field(para1, _identifier);
                var right = Expression.Field(para2, _identifier);
                var clearup = Expression.Call(left, left.Type.GetMethod("Clear"));
                var addrange = Expression.Call(left, left.Type.GetMethod("AddRange"), right);
                var body = Expression.Block(clearup, addrange);
                return Expression.Lambda<Action<SelectExpression, SelectExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        private static readonly Type TableReferenceExpressionType
            = typeof(SelectExpression).GetNestedType("TableReferenceExpression", System.Reflection.BindingFlags.NonPublic);

        private static readonly Func<SelectExpression, string, Expression> TableReferenceExpressionConstructor
            = TableReferenceExpressionType
                .GetConstructors()
                .Single()
                .CreateFactory()
              as Func<SelectExpression, string, Expression>;

        private static readonly Func<Expression, TableExpressionBase> TableReferenceExpressionAccessTable
            = ExpressionBuilder.Begin<Expression>()
                .As(TableReferenceExpressionType)
                .AccessProperty(nameof(TableReferenceExpression.Table))
                .Compile<Func<Expression, TableExpressionBase>>();

        private static readonly Func<Expression, string> TableReferenceExpressionAccessAlias
            = ExpressionBuilder.Begin<Expression>()
                .As(TableReferenceExpressionType)
                .AccessProperty(nameof(TableReferenceExpression.Alias))
                .Compile<Func<Expression, string>>();

        private readonly static Func<string, TableReferenceExpression, Type, RelationalTypeMapping, bool, ColumnExpression> ConcreteColumnExpressionConstructor
            = new Func<Expression<Func<string, TableReferenceExpression, Type, RelationalTypeMapping, bool, ColumnExpression>>>(delegate
            {
                var ctor = typeof(SelectExpression)
                    .GetNestedType("ConcreteColumnExpression", System.Reflection.BindingFlags.NonPublic)
                    .GetConstructors()
                    .Single(c => c.GetParameters().Length == 5);

                var pars = ctor.GetParameters()
                    .Select((par, i) => i == 1
                        ? Expression.Parameter(typeof(TableReferenceExpression), par.Name)
                        : Expression.Parameter(par.ParameterType, par.Name))
                    .ToArray();

                return Expression.Lambda<Func<string, TableReferenceExpression, Type, RelationalTypeMapping, bool, ColumnExpression>>(
                    Expression.New(
                        ctor,
                        pars.Select((par, i) => i == 1
                            ? Expression.Convert(
                                Expression.Property(par, nameof(TableReferenceExpression.InnerValue)),
                                TableReferenceExpressionType)
                            : (Expression)par)),
                    pars);
            })
            .Invoke().Compile();

        private sealed class AnyCastColumnExpression : ColumnExpression
        {
            public AnyCastColumnExpression(
                string name,
                TableExpressionBase table,
                Type type,
                RelationalTypeMapping typeMapping,
                bool nullable)
                : base(type, typeMapping)
            {
                Check.NotNull(name, nameof(name));
                Check.NotNull(table, nameof(table));
                Check.NotEmpty(table.Alias, "table.Alias");

                Name = name;
                Table = table;
                IsNullable = nullable;
            }

            public override string Name { get; }

            public override TableExpressionBase Table { get; }

            public override string TableAlias => Table.Alias;

            public override bool IsNullable { get; }

            public override ColumnExpression MakeNullable()
                => new AnyCastColumnExpression(Name, Table, Type, TypeMapping, nullable: true);

            protected override Expression VisitChildren(ExpressionVisitor visitor)
                => this;
        }

        public struct TableReferenceExpression
        {
            private readonly Expression _value;

            public Expression InnerValue
                => _value ?? throw new ArgumentNullException(nameof(_value));

            public TableExpressionBase Table
                => TableReferenceExpressionAccessTable(InnerValue);

            public string Alias
                => TableReferenceExpressionAccessAlias(InnerValue);

            public TableReferenceExpression(
                Expression expression)
            {
                _value = expression;
            }

            public TableReferenceExpression(
                SelectExpression selectExpression,
                string alias)
            {
                _value = TableReferenceExpressionConstructor(selectExpression, alias);
            }
        }
    }
}
