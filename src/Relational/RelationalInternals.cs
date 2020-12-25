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

    private static Action<SelectExpression, string> S_ApplyAlias()
    {
        var para1 = Expression.Parameter(typeof(SelectExpression), "select");
        var para2 = Expression.Parameter(typeof(string), "alias");
        var body = Expression.Assign(para1.AccessProperty("Alias"), para2);
        return Expression.Lambda<Action<SelectExpression, string>>(body, para1, para2).Compile();
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

    public static readonly Action<SelectExpression, string> ApplyAlias
        = S_ApplyAlias();
}
