using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore
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


        internal class Result<T>
        {
            public T Insert { get; set; }

            public T Update { get; set; }
        }


        #region private struct GenericJoinKey<>
#pragma warning disable IDE0060

        struct GenericJoinKey<T>
        {
            public GenericJoinKey(T _) { }
            public static bool operator ==(GenericJoinKey<T> _, GenericJoinKey<T> __) => false;
            public static bool operator !=(GenericJoinKey<T> _, GenericJoinKey<T> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct GenericJoinKey<T1, T2>
        {
            public GenericJoinKey(T1 _, T2 __) { }
            public static bool operator ==(GenericJoinKey<T1, T2> _, GenericJoinKey<T1, T2> __) => false;
            public static bool operator !=(GenericJoinKey<T1, T2> _, GenericJoinKey<T1, T2> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct GenericJoinKey<T1, T2, T3>
        {
            public GenericJoinKey(T1 _, T2 __, T3 ___) { }
            public static bool operator ==(GenericJoinKey<T1, T2, T3> _, GenericJoinKey<T1, T2, T3> __) => false;
            public static bool operator !=(GenericJoinKey<T1, T2, T3> _, GenericJoinKey<T1, T2, T3> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct GenericJoinKey<T1, T2, T3, T4>
        {
            public GenericJoinKey(T1 _, T2 __, T3 ___, T4 ____) { }
            public static bool operator ==(GenericJoinKey<T1, T2, T3, T4> _, GenericJoinKey<T1, T2, T3, T4> __) => false;
            public static bool operator !=(GenericJoinKey<T1, T2, T3, T4> _, GenericJoinKey<T1, T2, T3, T4> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

#pragma warning restore IDE0060
        #endregion
    }
}
