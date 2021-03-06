﻿using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.Relational")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.SqlServer")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.InMemory")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.PostgreSql")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.Sqlite")]
[assembly: InternalsVisibleTo("Microsoft.EntityFrameworkCore.Bulk.MySql")]
namespace Microsoft.EntityFrameworkCore.Utilities
{
    [DebuggerStepThrough]
    internal static class ReflectiveUtility
    {
        public const BindingFlags InstanceLevel
            = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static LambdaExpression UnwrapLambdaFromQuote(this Expression expression)
            => (LambdaExpression)(expression is UnaryExpression unary && expression.NodeType == ExpressionType.Quote
                ? unary.Operand
                : expression);

        public static bool IsConstantNull(this Expression expression)
            => expression is ConstantExpression constantExpression
                && constantExpression.Value == null;

        public static bool IsBodyConstantNull(this LambdaExpression lambdaExpression)
            => lambdaExpression.Body is ConstantExpression constantExpression
                && constantExpression.Value == null;

        public static Delegate CreateFactory(this ConstructorInfo constructor)
        {
            var @params = constructor.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType))
                .ToArray();

            return Expression.Lambda(Expression.New(constructor, @params), @params).Compile();
        }

        private static T Private<T>(this object obj, string privateField)
            => (T)obj?.GetType().GetField(privateField, InstanceLevel)?.GetValue(obj);

        public static Type UnwrapNullableType(this Type type)
            => Nullable.GetUnderlyingType(type) ?? type;

        public static bool IsAnonymousType(this Type type)
            => type.FullName.StartsWith("<>f__AnonymousType");

        public static bool IsNullableValueType(this Type type)
            => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        public static bool IsNullableType(this Type type)
            => !type.IsValueType || type.IsNullableValueType();

        public static Type MakeNullable(this Type type, bool nullable = true)
            => type.IsNullableType() == nullable
                ? type
                : nullable
                    ? typeof(Nullable<>).MakeGenericType(type)
                    : type.UnwrapNullableType();

        private static readonly Func<IQueryProvider, IQueryContextFactory> GetQueryContextFactory
            = ExpressionBuilder
                .Begin<IQueryProvider>()
                .As<EntityQueryProvider>()
                .AccessField("_queryCompiler")
                .As<QueryCompiler>()
                .AccessField("_queryContextFactory")
                .As<IQueryContextFactory>()
                .Compile<Func<IQueryProvider, IQueryContextFactory>>();

        private static QueryContextDependencies AccessDependencies(IQueryProvider queryProvider)
            => GetQueryContextFactory(queryProvider)
                .Private<QueryContextDependencies>("_dependencies");

        public static DbContext GetDbContext<T>(this IQueryable<T> query) where T : class
            => AccessDependencies(query.Provider).StateManager.Context;
    }
}
