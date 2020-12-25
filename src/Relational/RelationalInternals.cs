using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.SqlServer")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.PostgreSql")]
internal static partial class RelationalInternals
{
    const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags FindPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IRelationalCommandBuilder GenerateList<T>(
        this IRelationalCommandBuilder sql,
        IReadOnlyList<T> items,
        Action<T> generationAction,
        Action<IRelationalCommandBuilder> joinAction = null)
    {
        joinAction ??= (isb => isb.Append(", "));

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) joinAction(sql);
            generationAction(items[i]);
        }

        return sql;
    }

    public static IRelationalCommandBuilder Then(
        this IRelationalCommandBuilder sql,
        Action generationAction)
    {
        generationAction.Invoke();
        return sql;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static IReadOnlyList<IReadOnlyList<T>> AsList<T>(this T[,] ts)
    {
        return new TwoList<T>(ts);
    }

    private struct OneList<T> : IReadOnlyList<T>
    {
        private readonly T[,] inner;
        private readonly int id1;
        public T this[int index] => inner[id1, index];
        public int Count => inner.GetLength(1);
        public IEnumerator<T> GetEnumerator() => GetEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerable().GetEnumerator();

        public OneList(T[,] _inner, int id)
        {
            inner = _inner;
            id1 = id;
        }

        private IEnumerable<T> GetEnumerable()
        {
            for (int i = 0; i < Count; i++)
                yield return inner[id1, i];
        }
    }

    private class TwoList<T> : IReadOnlyList<IReadOnlyList<T>>
    {
        private readonly T[,] inner;
        public IReadOnlyList<T> this[int index] => new OneList<T>(inner, index);
        public int Count => inner.GetLength(0);
        public IEnumerator<IReadOnlyList<T>> GetEnumerator() => GetEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerable().GetEnumerator();
        public TwoList(T[,] _inner) => inner = _inner;

        private IEnumerable<IReadOnlyList<T>> GetEnumerable()
        {
            for (int i = 0; i < Count; i++)
                yield return new OneList<T>(inner, i);
        }
    }

    private static Action<SelectExpression, SqlExpression> S_ApplyPredicate()
    {
        var para1 = Expression.Parameter(typeof(SelectExpression), "select");
        var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
        var body = Expression.Assign(para1.AccessProperty("Predicate"), para2);
        return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2).Compile();
    }

    private static Action<TableExpressionBase, string> S_ApplyAlias()
    {
        var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
        var para2 = Expression.Parameter(typeof(string), "alias");
        var body = Expression.Assign(para1.AccessProperty("Alias"), para2);
        return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2).Compile();
    }

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

#if EFCORE50
    public static readonly Func<ITableBase, TableExpression> CreateTableExpression
        = typeof(TableExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;
#elif EFCORE31
    public static readonly Func<string, string, string, TableExpression> CreateTableExpression
        = typeof(TableExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;
#endif

    public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> CreateSqlParameterExpression
        = typeof(SqlParameterExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;

    public static readonly Func<SqlExpression, string, ProjectionExpression> CreateProjectionExpression
        = typeof(ProjectionExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;

    public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> CreateColumnExpression
        = typeof(ColumnExpression)
            .GetConstructors(bindingFlags)
            .Single(c => c.GetParameters().Length == 5)
            .CreateFactory() as dynamic;

    public static readonly Func<TypeMappedRelationalParameter, RelationalTypeMapping> AccessRelationalTypeMapping
        = Internals.CreateLambda<TypeMappedRelationalParameter, RelationalTypeMapping>(
            param => param.AccessProperty(nameof(RelationalTypeMapping)))
        .Compile();

    public static readonly Func<TypeMappedRelationalParameter, bool?> AccessIsNullable
        = Internals.CreateLambda<TypeMappedRelationalParameter, bool?>(
            param => param.AccessProperty("IsNullable"))
        .Compile();

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

    public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
        = Internals.CreateLambda<SelectExpression, IDictionary<ProjectionMember, Expression>>(
            param => param.AccessField("_projectionMapping"))
        .Compile();

    public static readonly Action<QuerySqlGenerator> InitQuerySqlGenerator
        = S_InitQuerySqlGenerator();

    public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
        = S_ApplyPredicate();

    public static readonly Action<TableExpressionBase, string> ApplyAlias
        = S_ApplyAlias();

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
