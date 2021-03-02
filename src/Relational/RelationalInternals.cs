using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.SqlServer")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.PostgreSql")]
internal static partial class RelationalInternals
{
    const BindingFlags FindPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private static Action<QuerySqlGenerator> S_InitQuerySqlGenerator()
    {
        var para = Expression.Parameter(typeof(QuerySqlGenerator), "g");
        var builder = para.AccessField("_relationalCommandBuilder");
        var builderFactory = para.AccessField("_relationalCommandBuilderFactory");
        var method = typeof(IRelationalCommandBuilderFactory).GetMethod("Create");
        var right = Expression.Call(builderFactory, method);
        var body = Expression.Assign(builder, right);
        return Expression.Lambda<Action<QuerySqlGenerator>>(body, para).Compile();
    }

    private static Action<QuerySqlGenerator, FromSqlExpression> S_GenerateFromSql()
    {
        var para = Expression.Parameter(typeof(QuerySqlGenerator), "g");
        var para2 = Expression.Parameter(typeof(FromSqlExpression), "s");
        var method = typeof(QuerySqlGenerator).GetMethod("GenerateFromSql", FindPrivate);
        var body = Expression.Call(para, method, para2);
        return Expression.Lambda<Action<QuerySqlGenerator, FromSqlExpression>>(body, para, para2).Compile();
    }

    public static readonly Func<RelationalQueryableMethodTranslatingExpressionVisitor, RelationalSqlTranslatingExpressionVisitor> AccessTranslator
        = Internals.CreateLambda<RelationalQueryableMethodTranslatingExpressionVisitor, RelationalSqlTranslatingExpressionVisitor>(param => param.AccessField("_sqlTranslator")).Compile();

    public static readonly Func<IQueryProvider, QueryContextDependencies> AccessDependencies
        = Internals.CreateLambda<IQueryProvider, QueryContextDependencies>(param => param
            .As<EntityQueryProvider>()
            .AccessField("_queryCompiler")
            .As<QueryCompiler>()
            .AccessField("_queryContextFactory")
            .As<RelationalQueryContextFactory>()
            .AccessField("_dependencies")
            .As<QueryContextDependencies>())
        .Compile();

    public static readonly Action<QuerySqlGenerator> InitQuerySqlGenerator
        = S_InitQuerySqlGenerator();

    public static readonly Action<QuerySqlGenerator, FromSqlExpression> ApplyGenerateFromSql
        = S_GenerateFromSql();

    private static readonly MethodInfo s_Join_TOuter_TInner_TKey_TResult_5
        = new Func<IQueryable<object>, IEnumerable<object>, Expression<Func<object, object>>, Expression<Func<object, object>>, Expression<Func<object, object, object>>, IQueryable<object>>(Queryable.Join).GetMethodInfo().GetGenericMethodDefinition();

    public static IQueryable<TResult> Join<TOuter, TInner, TResult>(this IQueryable<TOuter> outer, IEnumerable<TInner> inner, Type joinKeyType, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, Expression<Func<TOuter, TInner, TResult>> resultSelector)
    {
        if (outer == null || inner == null || outerKeySelector == null || innerKeySelector == null || resultSelector == null)
            throw new ArgumentNullException(nameof(resultSelector));
        var innerExpression = inner is IQueryable<TInner> q ? q.Expression : Expression.Constant(inner, typeof(IEnumerable<TInner>));
        return outer.Provider.CreateQuery<TResult>(
            Expression.Call(
                null,
                s_Join_TOuter_TInner_TKey_TResult_5.MakeGenericMethod(typeof(TOuter), typeof(TInner), joinKeyType, typeof(TResult)), outer.Expression, innerExpression, Expression.Quote(outerKeySelector), Expression.Quote(innerKeySelector), Expression.Quote(resultSelector)));
    }
}
