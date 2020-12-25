using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class AnonymousObjectExpressionFactory
    {
        public static SqlParameterExpression ToSql(this ParameterExpression par, RelationalTypeMapping map)
        {
            return Internals.CreateSqlParameterExpression(par, map);
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
    }
}
