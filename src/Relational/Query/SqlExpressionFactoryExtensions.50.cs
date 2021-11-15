using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
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
        public static IRelationalCommand RentAndPopulateRelationalCommand(
            this RelationalCommandCache relationalCommandCache,
            QueryContext queryContext)
            => relationalCommandCache.GetRelationalCommand(queryContext.ParameterValues);

        public static void ReplaceProjection(
            this SelectExpression selectExpression,
            IReadOnlyDictionary<ProjectionMember, Expression> projectionMapping)
            => selectExpression.ReplaceProjectionMapping((IDictionary<ProjectionMember, Expression>)projectionMapping);

        internal static Expression AddCrossJoinForMerge(
            this SelectExpression outerSelectExpression,
            ShapedQueryExpression innerSource,
            Expression outerShaper)
            => outerSelectExpression.AddCrossJoin(innerSource, outerShaper);

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

        public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> ColumnExpressionConstructor
            = typeof(ColumnExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression>;

        public static readonly Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression> SelectExpressionConstructor
            = typeof(SelectExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>;

        public static readonly Func<SelectExpression, IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>>> AccessEntityProjectionCache
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_entityProjectionCache")
                .Compile<Func<SelectExpression, IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>>>>();

        public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_projectionMapping")
                .Compile<Func<SelectExpression, IDictionary<ProjectionMember, Expression>>>();

        public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Predicate"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<SelectExpression, SqlExpression> ApplyHaving
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Having"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<TableExpressionBase, string> ApplyAlias
            = new Func<Expression<Action<TableExpressionBase, string>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
                var para2 = Expression.Parameter(typeof(string), "alias");
                var body = Expression.Assign(Expression.Property(para1, "Alias"), para2);
                return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<SelectExpression, SelectExpression> ApplyCopyIdentifiersFrom
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
    }
}
