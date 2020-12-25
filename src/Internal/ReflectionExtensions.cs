using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class ReflectionExtensions
{
    const BindingFlags FindPrivate = BindingFlags.Instance | BindingFlags.NonPublic;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Type UnwrapNullableType(this Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static bool IsAnonymousType(this Type type)
        => type.FullName.StartsWith("<>f__AnonymousType");
}