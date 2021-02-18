using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class AnonymousObjectExpressionFactory
    {
        public static SqlParameterExpression ToSql(this ParameterExpression par, RelationalTypeMapping map)
        {
            return RelationalInternals.CreateSqlParameterExpression(par, map);
        }

        public static void GetTransparentIdentifier(
            ParameterExpression targetParam, IKey keys,
            ParameterExpression sourceParam, IReadOnlyList<MemberBinding> sourceBindings,
            out Type transparentIdentifierType,
            out LambdaExpression targetKeySelector,
            out LambdaExpression sourceKeySelector)
        {
            transparentIdentifierType = keys.Properties.Count switch
            {
                1 => typeof(TransparentIdentifier<>),
                2 => typeof(TransparentIdentifier<,>),
                3 => typeof(TransparentIdentifier<,,>),
                4 => typeof(TransparentIdentifier<,,,>),
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

            transparentIdentifierType = transparentIdentifierType.MakeGenericType(types);
            var constructor = transparentIdentifierType.GetConstructors().Single();
            var innerNew = Expression.New(constructor, inner);
            var outerNew = Expression.New(constructor, outer);
            targetKeySelector = Expression.Lambda(innerNew, targetParam);
            sourceKeySelector = Expression.Lambda(outerNew, sourceParam);
        }

        public static NewExpression Create(Type t)
        {
            var ctor = t.GetConstructors().Single();
            return Expression.New(
                constructor: ctor,
                arguments: ctor.GetParameters().Select((p, i) => GetConstant(p.ParameterType, i)),
                members: t.GetProperties());
        }

        public static ConstantExpression GetConstant(Type type1, int i)
        {
            var type = type1;
            if (type1.IsGenericType && type1.GetGenericTypeDefinition() == typeof(Nullable<>))
                type = type1.GetGenericArguments().Single();
            if (type == typeof(int)) return Expression.Constant(i, type1);
            if (type == typeof(long)) return Expression.Constant((long)i, type1);
            if (type == typeof(string)) return Expression.Constant(i.ToString(), type1);
            if (type == typeof(double)) return Expression.Constant((double)i, type1);
            if (type == typeof(Guid)) return Expression.Constant(Guid.Parse(i.ToString().PadLeft(32, '0')), type1);
            if (type == typeof(DateTime)) return Expression.Constant(new DateTime(i + 1, 0, 0), type1);
            if (type == typeof(DateTimeOffset)) return Expression.Constant(new DateTimeOffset(i + 1, 0, 0, 0, 0, 0, TimeSpan.Zero), type1);
            if (type == typeof(TimeSpan)) return Expression.Constant(new TimeSpan(i + 1, 0, 0), type1);
            throw new NotSupportedException();
        }

        public static int ReadBack(this SqlConstantExpression c)
        {
            var type = c.Type;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                type = type.GetGenericArguments().Single();
            var i = c.Value;
            if (type == typeof(int)) return (int)i;
            if (type == typeof(long)) return (int)((long)i);
            if (type == typeof(string)) return int.Parse((string)i);
            if (type == typeof(double)) return (int)((double)i + 0.5);
            if (type == typeof(Guid)) return int.Parse(((Guid)i).ToString("N"));
            if (type == typeof(DateTime)) return ((DateTime)i).Year - 1;
            if (type == typeof(DateTimeOffset)) return ((DateTimeOffset)i).Year - 1;
            if (type == typeof(TimeSpan)) return ((TimeSpan)i).Hours - 1;
            throw new NotSupportedException();
        }

        #region struct TransparentIdentifier
#pragma warning disable IDE0060

        struct TransparentIdentifier<T>
        {
            public TransparentIdentifier(T _) { }
            public static bool operator ==(TransparentIdentifier<T> _, TransparentIdentifier<T> __) => false;
            public static bool operator !=(TransparentIdentifier<T> _, TransparentIdentifier<T> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct TransparentIdentifier<T1, T2>
        {
            public TransparentIdentifier(T1 _, T2 __) { }
            public static bool operator ==(TransparentIdentifier<T1, T2> _, TransparentIdentifier<T1, T2> __) => false;
            public static bool operator !=(TransparentIdentifier<T1, T2> _, TransparentIdentifier<T1, T2> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct TransparentIdentifier<T1, T2, T3>
        {
            public TransparentIdentifier(T1 _, T2 __, T3 ___) { }
            public static bool operator ==(TransparentIdentifier<T1, T2, T3> _, TransparentIdentifier<T1, T2, T3> __) => false;
            public static bool operator !=(TransparentIdentifier<T1, T2, T3> _, TransparentIdentifier<T1, T2, T3> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

        struct TransparentIdentifier<T1, T2, T3, T4>
        {
            public TransparentIdentifier(T1 _, T2 __, T3 ___, T4 ____) { }
            public static bool operator ==(TransparentIdentifier<T1, T2, T3, T4> _, TransparentIdentifier<T1, T2, T3, T4> __) => false;
            public static bool operator !=(TransparentIdentifier<T1, T2, T3, T4> _, TransparentIdentifier<T1, T2, T3, T4> __) => false;
            public override bool Equals(object obj) => false;
            public override int GetHashCode() => 0;
        }

#pragma warning restore IDE0060
        #endregion
    }
}
