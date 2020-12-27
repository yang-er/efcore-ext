using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.Relational")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.SqlServer")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.InMemory")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.PostgreSql")]
internal static partial class Internals
{
    public const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public const BindingFlags FindPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    public static MemberExpression AccessPrivateMember(this Expression expression, string name)
    {
        var member = expression.Type.GetMember(name, bindingFlags).Single();
        return Expression.MakeMemberAccess(expression, member);
    }

    public static MemberExpression AccessField(this Expression expression, string name)
    {
        var member = expression.Type.GetField(name, bindingFlags);
        return Expression.MakeMemberAccess(expression, member);
    }

    public static MemberExpression AccessProperty(this Expression expression, string name)
    {
        var member = expression.Type.GetProperty(name, bindingFlags);
        return Expression.MakeMemberAccess(expression, member);
    }

    public static Expression As<T>(this Expression expression)
    {
        return Expression.Convert(expression, typeof(T));
    }

    public static Expression<Func<TIn, TOut>> CreateLambda<TIn, TOut>(
        Func<ParameterExpression, Expression> bodyBuilder)
    {
        var param = Expression.Parameter(typeof(TIn), "arg1");
        return Expression.Lambda<Func<TIn, TOut>>(bodyBuilder(param), param);
    }

    public static Delegate CreateFactory(this ConstructorInfo ctor)
    {
        var @params = ctor.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        return Expression.Lambda(Expression.MemberInit(Expression.New(ctor, @params)), @params).Compile();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static object Private(this object obj, string privateField)
    {
        return obj?.GetType().GetField(privateField, FindPrivate)?.GetValue(obj);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static T Private<T>(this object obj, string privateField)
    {
        return (T)obj?.GetType().GetField(privateField, FindPrivate)?.GetValue(obj);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Type UnwrapNullableType(this Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static bool IsAnonymousType(this Type type)
        => type.FullName.StartsWith("<>f__AnonymousType");
}

internal sealed class NotNullAttribute : Attribute
{
}

internal sealed class AllowNullAttribute : Attribute
{
}

internal sealed class DisallowNullAttribute : Attribute
{
}

internal static class NetStandardCompatibilityExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
    {
        key = kvp.Key;
        value = kvp.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char AtEnd(this string str, int end)
    {
        return str[str.Length - end];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key)
    {
        return dict.TryGetValue(key, out var value) ? value : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> ts)
    {
        var hs = new HashSet<T>();
        foreach (var item in ts) hs.Add(item);
        return hs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Expression<Func<T4, T3>> Combine<T1, T2, T3, T4>(
        this Expression<Func<T1, T2, T3>> expression,
        T4 objectTemplate,
        Expression<Func<T4, T1>> place1,
        Expression<Func<T4, T2>> place2)
    {
        if (expression == null) return null;
        var parameter = place1.Parameters.Single();
        var hold1 = place1.Body;
        var hold2 = new ParameterReplaceVisitor(
            (place2.Parameters[0], parameter))
            .Visit(place2.Body);
        var newBody = new ParameterReplaceVisitor(
            (expression.Parameters[0], hold1),
            (expression.Parameters[1], hold2))
            .Visit(expression.Body);
        return Expression.Lambda<Func<T4, T3>>(newBody, parameter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        Expression<Func<T, bool>> expression)
    {
        if (expression == null) return source;
        return source.Where(expression);
    }

    private static readonly Func<IQueryProvider, IQueryContextFactory> GetQueryContextFactory
        = Internals.CreateLambda<IQueryProvider, IQueryContextFactory>(param => param
             .As<EntityQueryProvider>()
             .AccessField("_queryCompiler")
             .As<QueryCompiler>()
             .AccessField("_queryContextFactory")
             .As<IQueryContextFactory>())
        .Compile();

    private static QueryContextDependencies AccessDependencies(IQueryProvider queryProvider)
        => GetQueryContextFactory(queryProvider)
            .Private<QueryContextDependencies>("_dependencies");

    public static DbContext GetDbContext<T>(this IQueryable<T> query) where T : class
    {
        return AccessDependencies(query.Provider).StateManager.Context;
    }

    public static DbContext GetDbContext<T>(this DbSet<T> set) where T : class
    {
        return set.GetService<ICurrentDbContext>().Context;
    }
}

internal class ParameterReplaceVisitor : ExpressionVisitor
{
    public ParameterReplaceVisitor(params (ParameterExpression, Expression)[] changes)
    {
        Changes = changes.ToDictionary(k => k.Item1, k => k.Item2);
    }

    public Dictionary<ParameterExpression, Expression> Changes { get; }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return Changes.GetValueOrDefault(node) ?? node;
    }
}