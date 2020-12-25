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
internal static partial class Internals
{
    const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags FindPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

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