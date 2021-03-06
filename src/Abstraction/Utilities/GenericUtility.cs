using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Utilities
{
    internal static class GenericUtility
    {
        public static bool Preserve(Type type)
            => type.IsNested && type.DeclaringType == typeof(GenericUtility);

        public static void CreateJoinKey(
            ParameterExpression targetParam, IKey keys,
            ParameterExpression sourceParam, IReadOnlyList<MemberBinding> sourceBindings,
            out Type joinKeyType,
            out LambdaExpression targetKeySelector,
            out LambdaExpression sourceKeySelector)
        {
            joinKeyType = keys.Properties.Count switch
            {
                1 => typeof(GenericJoinKey<>),
                2 => typeof(GenericJoinKey<,>),
                3 => typeof(GenericJoinKey<,,>),
                4 => typeof(GenericJoinKey<,,,>),
                _ => throw new NotSupportedException($"Key with {keys.Properties} properties is not supported yet."),
            };

            var inner = new Expression[keys.Properties.Count];
            var outer = new Expression[keys.Properties.Count];
            var types = new Type[keys.Properties.Count];
            for (int i = 0; i < keys.Properties.Count; i++)
            {
                var prop = keys.Properties[i];
                var member = (MemberInfo)prop.PropertyInfo ?? prop.FieldInfo;
                if (prop.IsShadowProperty())
                    throw new NotSupportedException("Shadow property in primary key is not supported yet.");

                var binding = sourceBindings.OfType<MemberAssignment>().FirstOrDefault(m => m.Member == member);
                if (binding == null)
                    throw new ArgumentException("The outer member binding doesn't contains the property in primary key.");

                inner[i] = Expression.MakeMemberAccess(targetParam, member);
                outer[i] = binding.Expression;
                types[i] = prop.ClrType;
            }

            joinKeyType = joinKeyType.MakeGenericType(types);
            var constructor = joinKeyType.GetConstructors().Single();
            var innerNew = Expression.New(constructor, inner);
            var outerNew = Expression.New(constructor, outer);
            targetKeySelector = Expression.Lambda(innerNew, targetParam);
            sourceKeySelector = Expression.Lambda(outerNew, sourceParam);
        }

        struct GenericJoinKey<T> : IEquatable<GenericJoinKey<T>>
            where T : IEquatable<T>
        {
            readonly T Value1;
            public GenericJoinKey(T _) => Value1 = _;
            public static bool operator ==(GenericJoinKey<T> _, GenericJoinKey<T> __) => _.Equals(__);
            public static bool operator !=(GenericJoinKey<T> _, GenericJoinKey<T> __) => !_.Equals(__);
            public override bool Equals(object obj) => obj is GenericJoinKey<T> other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Value1);
            public bool Equals(GenericJoinKey<T> other) => Value1.Equals(other.Value1);
        }

        struct GenericJoinKey<T1, T2> : IEquatable<GenericJoinKey<T1, T2>>
            where T1 : IEquatable<T1>
            where T2 : IEquatable<T2>
        {
            readonly T1 Value1; readonly T2 Value2;
            public GenericJoinKey(T1 _, T2 __) => (Value1, Value2) = (_, __);
            public static bool operator ==(GenericJoinKey<T1, T2> _, GenericJoinKey<T1, T2> __) => _.Equals(__);
            public static bool operator !=(GenericJoinKey<T1, T2> _, GenericJoinKey<T1, T2> __) => !_.Equals(__);
            public override bool Equals(object obj) => obj is GenericJoinKey<T1, T2> other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Value1, Value2);
            public bool Equals(GenericJoinKey<T1, T2> other) => Value1.Equals(other.Value1) && Value2.Equals(other.Value2);
        }

        struct GenericJoinKey<T1, T2, T3> : IEquatable<GenericJoinKey<T1, T2, T3>>
            where T1 : IEquatable<T1>
            where T2 : IEquatable<T2>
            where T3 : IEquatable<T3>
        {
            readonly T1 Value1; readonly T2 Value2; readonly T3 Value3;
            public GenericJoinKey(T1 _, T2 __, T3 ___) => (Value1, Value2, Value3) = (_, __, ___);
            public static bool operator ==(GenericJoinKey<T1, T2, T3> _, GenericJoinKey<T1, T2, T3> __) => _.Equals(__);
            public static bool operator !=(GenericJoinKey<T1, T2, T3> _, GenericJoinKey<T1, T2, T3> __) => !_.Equals(__);
            public override bool Equals(object obj) => obj is GenericJoinKey<T1, T2, T3> other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Value1, Value2, Value3);
            public bool Equals(GenericJoinKey<T1, T2, T3> other) => Value1.Equals(other.Value1) && Value2.Equals(other.Value2) && Value3.Equals(other.Value3);
        }

        struct GenericJoinKey<T1, T2, T3, T4> : IEquatable<GenericJoinKey<T1, T2, T3, T4>>
            where T1 : IEquatable<T1>
            where T2 : IEquatable<T2>
            where T3 : IEquatable<T3>
            where T4 : IEquatable<T4>
        {
            readonly T1 Value1; readonly T2 Value2; readonly T3 Value3; readonly T4 Value4;
            public GenericJoinKey(T1 _, T2 __, T3 ___, T4 ____) => (Value1, Value2, Value3, Value4) = (_, __, ___, ____);
            public static bool operator ==(GenericJoinKey<T1, T2, T3, T4> _, GenericJoinKey<T1, T2, T3, T4> __) => _.Equals(__);
            public static bool operator !=(GenericJoinKey<T1, T2, T3, T4> _, GenericJoinKey<T1, T2, T3, T4> __) => !_.Equals(__);
            public override bool Equals(object obj) => obj is GenericJoinKey<T1, T2, T3, T4> other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Value1, Value2, Value3, Value4);
            public bool Equals(GenericJoinKey<T1, T2, T3, T4> other) => Value1.Equals(other.Value1) && Value2.Equals(other.Value2) && Value3.Equals(other.Value3) && Value4.Equals(other.Value4);
        }
    }
}
