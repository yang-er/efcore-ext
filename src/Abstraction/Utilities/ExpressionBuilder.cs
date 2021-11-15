using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Utilities
{
    internal class ExpressionBuilder
    {
        public const BindingFlags InstanceLevel
            = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public ParameterExpression Parameter { get; }

        public Expression Current { get; private set; }

        private  ExpressionBuilder(ParameterExpression parameterExpression)
        {
            Parameter = parameterExpression;
            Current = parameterExpression;
        }

        public ExpressionBuilder AccessField(string fieldName)
        {
            Current = Expression.Field(
                Current,
                Current.Type.GetField(fieldName, InstanceLevel));

            return this;
        }

        public ExpressionBuilder AccessProperty(string propertyName)
        {
            Current = Expression.Property(
                Current,
                Current.Type.GetProperty(propertyName, InstanceLevel));

            return this;
        }

        public ExpressionBuilder As<T>()
        {
            Current = Expression.Convert(Current, typeof(T));
            return this;
        }

        public ExpressionBuilder As(Type type)
        {
            Current = Expression.Convert(Current, type);
            return this;
        }

        public static ExpressionBuilder Begin<T>()
        {
            return new ExpressionBuilder(Expression.Parameter(typeof(T), "args0"));
        }

        public TDelegate Compile<TDelegate>()
        {
            return Expression.Lambda<TDelegate>(Current, Parameter).Compile();
        }
    }
}
