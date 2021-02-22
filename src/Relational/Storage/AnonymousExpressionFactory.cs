using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public interface IAnonymousExpressionFactory
    {
        AnonymousExpressionType GetType(Type type);
    }

    public class AnonymousExpressionField
    {
        public PropertyInfo PropertyInfo { get; }

        public string Name { get; }

        public RelationalTypeMapping TypeMapping { get; }

        public bool Nullable { get; }

        internal AnonymousExpressionField(
            PropertyInfo propertyInfo,
            string name,
            RelationalTypeMapping typeMapping,
            bool nullable)
        {
            Check.NotNull(propertyInfo, nameof(propertyInfo));
            Check.NotNull(name, nameof(name));
            Check.NotNull(typeMapping, nameof(typeMapping));

            PropertyInfo = propertyInfo;
            Name = name;
            TypeMapping = typeMapping;
            Nullable = nullable;
        }

        public ColumnExpression CreateColumn(
            TableExpressionBase table)
        {
            return RelationalInternals.CreateColumnExpression(
                Name,
                table,
                PropertyInfo.PropertyType,
                TypeMapping,
                Nullable);
        }

        public Expression CreateProjectionBinding(
            SelectExpression queryExpression,
            ProjectionMember projectionMember)
        {
            var targetType = PropertyInfo.PropertyType;

            Expression expression =
                new ProjectionBindingExpression(
                    queryExpression,
                    projectionMember,
                    targetType.MakeNullable());

            if (expression.Type != targetType)
            {
                Check.DebugAssert(targetType.MakeNullable() == expression.Type, "expression.Type must be nullable of targetType");
                expression = Expression.Convert(expression, targetType);
            }

            return expression;
        }
    }

    public class AnonymousExpressionType
    {
        private readonly Action<DbCommand, string, object> _addDbParameter;

        public IReadOnlyList<AnonymousExpressionField> Fields { get; }

        public Type ClrType { get; }

        public ConstructorInfo Constructor { get; }

        public NewExpression NewExpression { get; }

        internal AnonymousExpressionType(
            IReadOnlyList<AnonymousExpressionField> fields,
            Type clrType,
            NewExpression newExpression,
            Action<DbCommand, string, object> addDbParameter)
        {
            Check.NotNull(fields, nameof(fields));
            Check.NotNull(clrType, nameof(clrType));
            Check.NotNull(newExpression, nameof(newExpression));
            Check.NotNull(addDbParameter, nameof(addDbParameter));

            Fields = fields;
            ClrType = clrType;
            NewExpression = newExpression;
            Constructor = newExpression.Constructor;
            _addDbParameter = addDbParameter;
        }

        public void AddDbParameter(DbCommand command, string prefix, object entity)
        {
            _addDbParameter(command, prefix, entity);
        }

        public NewExpression CreateShaper(IEnumerable<Expression> parameters)
        {
            return Expression.New(Constructor, parameters, ClrType.GetProperties());
        }
    }

    public class AnonymousExpressionFactory : IAnonymousExpressionFactory
    {
        private static readonly ParameterExpression _command = Expression.Parameter(typeof(DbCommand), "command");
        private static readonly ParameterExpression _realEntity = Expression.Parameter(typeof(object), "entity");
        private static readonly ParameterExpression _prefix = Expression.Parameter(typeof(string), "prefix");
        private static readonly MemberExpression _commandParameter = Expression.Property(_command, nameof(DbCommand.Parameters));
        private static readonly MethodInfo _parameterAdd = typeof(DbParameterCollection).GetMethod(nameof(DbParameterCollection.Add));
        private static readonly MethodInfo _typeMappingCreateParameter = typeof(RelationalTypeMapping).GetMethod(nameof(RelationalTypeMapping.CreateParameter));
        private static readonly MethodInfo _stringConcat = new Func<string, string, string>(string.Concat).GetMethodInfo();

        private readonly IRelationalTypeMappingSource _typeMappingSource;
        private readonly ConcurrentDictionary<Type, AnonymousExpressionType> _cachedTypes;

        public AnonymousExpressionFactory(
            IRelationalTypeMappingSource typeMappingSource)
        {
            Check.NotNull(typeMappingSource, nameof(typeMappingSource));

            _typeMappingSource = typeMappingSource;
            _cachedTypes = new ConcurrentDictionary<Type, AnonymousExpressionType>();
        }

        public AnonymousExpressionType GetType(Type type)
        {
            Check.NotNull(type, nameof(type));
            return _cachedTypes.GetOrAdd(type, GetTypeCore);
        }

        AnonymousExpressionType GetTypeCore(Type anonymousType)
        {
            var properties = anonymousType.GetProperties();
            var ctor = anonymousType.GetConstructors().Single();
            var parameters = ctor.GetParameters();
            var arguments = new Expression[parameters.Length];
            var fields = new AnonymousExpressionField[parameters.Length];
            var dynamicAdds = new List<Expression>();
            var sourceParam = Expression.Convert(_realEntity, anonymousType);

            for (int i = 0; i < properties.Length; i++)
            {
                Check.DebugAssert(
                    parameters[i].ParameterType == properties[i].PropertyType
                    && parameters[i].Name == properties[i].Name,
                    "Constructor and property should have the same sequence.");

                var type = parameters[i].ParameterType;
                var typeMapping = _typeMappingSource.FindMapping(type);
                if (typeMapping == null)
                {
                    throw new NotSupportedException(
                        $"Type of {type} is not supported in anonymous type.");
                }

                arguments[i] = Expression.Default(type.UnwrapNullableType());

                fields[i] = new AnonymousExpressionField(
                    propertyInfo: properties[i],
                    name: properties[i].Name,
                    typeMapping: typeMapping,
                    nullable: type.IsNullableType());

                dynamicAdds.Add(
                    Expression.Call(
                        _commandParameter, // command.Parameters
                        _parameterAdd, // .Add(
                        Expression.Call(
                            Expression.Constant(typeMapping, typeof(RelationalTypeMapping)), // RelationalTypeMapping
                            _typeMappingCreateParameter, // .CreateParameter(
                            _command, // command,
                            Expression.Call(_stringConcat, _prefix, Expression.Constant("_" + i)), // Name,
                            Expression.Convert(Expression.Property(sourceParam, properties[i]), typeof(object)), // value,
                            Expression.Constant(type.IsNullableType(), typeof(bool?))))); // IsNullable));
            }

            var accessor = Expression
                .Lambda<Action<DbCommand, string, object>>(
                    Expression.Block(dynamicAdds),
                    _command, _prefix, _realEntity)
                .Compile();

            var @new = Expression.New(ctor, arguments, properties);

            return new AnonymousExpressionType(fields, anonymousType, @new, accessor);
        }
    }
}
