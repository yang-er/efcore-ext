using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    internal class SearchConditionBooleanGuard : IDisposable
    {
        // bool value: True for predicate sections, false for value sections.
        public delegate void Setter(ExpressionVisitor expressionVisitor, bool isSearchCondition);
        public delegate bool Getter(ExpressionVisitor expressionVisitor);
        public static readonly Dictionary<Type, (Getter, Setter)> VisitorTypes = new Dictionary<Type, (Getter, Setter)>();

        private bool _disposed;
        private bool _originalValue;
        private ExpressionVisitor _visitor;
        private Setter _setter;

        public static void AddTypeField(Type type, string memberName)
        {
            lock (VisitorTypes)
            {
                if (VisitorTypes.ContainsKey(type)) return;
                var param = Expression.Parameter(typeof(ExpressionVisitor));
                var value = Expression.Parameter(typeof(bool));
                var convert = Expression.Convert(param, type);
                var member = type.GetField(memberName, ReflectiveUtility.InstanceLevel);
                var field = Expression.Field(convert, member);
                var getter = Expression.Lambda<Getter>(field, param);
                var setter = Expression.Lambda<Setter>(Expression.Assign(field, value), param, value);
                VisitorTypes.Add(type, (getter.Compile(), setter.Compile()));
            }
        }

        private SearchConditionBooleanGuard(ExpressionVisitor visitor, Setter setter, bool origin)
        {
            _visitor = visitor;
            _setter = setter;
            _originalValue = origin;
        }

        public static IDisposable With(ExpressionVisitor expressionVisitor, bool isSearchCondition, bool? outside = default)
        {
            var type = expressionVisitor.GetType();
            if (!VisitorTypes.ContainsKey(type)) return null;

            var (getter, setter) = VisitorTypes[type];
            bool current = getter.Invoke(expressionVisitor);
            if (outside.HasValue && outside.Value != current)
            {
                throw new InvalidOperationException("State corrupt.");
            }

            setter.Invoke(expressionVisitor, isSearchCondition);
            return new SearchConditionBooleanGuard(expressionVisitor, setter, current);
        }

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _setter?.Invoke(_visitor, _originalValue);
                }

                _visitor = null;
                _setter = null;
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
