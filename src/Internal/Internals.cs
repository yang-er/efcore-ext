using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

internal static class Internals
{
    const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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

    private static Action<SelectExpression, SqlExpression> S_ApplyPredicate()
    {
        var para1 = Expression.Parameter(typeof(SelectExpression), "select");
        var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
        var body = Expression.Assign(para1.AccessProperty("Predicate"), para2);
        return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2).Compile();
    }

    private static Action<SelectExpression, string> S_ApplyAlias()
    {
        var para1 = Expression.Parameter(typeof(SelectExpression), "select");
        var para2 = Expression.Parameter(typeof(string), "alias");
        var body = Expression.Assign(para1.AccessProperty("Alias"), para2);
        return Expression.Lambda<Action<SelectExpression, string>>(body, para1, para2).Compile();
    }

    private static Action<QuerySqlGenerator> S_InitQuerySqlGenerator()
    {
        var para = Expression.Parameter(typeof(QuerySqlGenerator), "g");
        var builder = para.AccessField("_relationalCommandBuilder");
        var builderFactory = para.AccessField("_relationalCommandBuilderFactory");
        var method = typeof(IRelationalCommandBuilderFactory).GetMethod("Create");
        var right = Expression.Call(builderFactory, method);
        var body = Expression.Assign(builder, right);
        return Expression.Lambda<Action<QuerySqlGenerator>>(body, para).Compile();
    }



    public static readonly Func<string, string, string, TableExpression> CreateTableExpression
        = typeof(TableExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;

    public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> CreateSqlParameterExpression
        = typeof(SqlParameterExpression)
            .GetConstructors(bindingFlags)[0]
            .CreateFactory() as dynamic;

    public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> CreateColumnExpression
        = typeof(ColumnExpression)
            .GetConstructors(bindingFlags)
            .Single(c => c.GetParameters().Length == 5)
            .CreateFactory() as dynamic;

    public static readonly Func<TypeMappedRelationalParameter, RelationalTypeMapping> AccessRelationalTypeMapping
        = CreateLambda<TypeMappedRelationalParameter, RelationalTypeMapping>(
            param => param.AccessProperty(nameof(RelationalTypeMapping)))
        .Compile();

    public static readonly Func<TypeMappedRelationalParameter, bool?> AccessIsNullable
        = CreateLambda<TypeMappedRelationalParameter, bool?>(
            param => param.AccessProperty("IsNullable"))
        .Compile();

    public static readonly Func<IQueryProvider, QueryContextDependencies> AccessDependencies
        = CreateLambda<IQueryProvider, QueryContextDependencies>(param => param
            .As<EntityQueryProvider>()
            .AccessField("_queryCompiler")
            .As<QueryCompiler>()
            .AccessField("_queryContextFactory")
            .As<RelationalQueryContextFactory>()
            .AccessField("_dependencies")
            .As<QueryContextDependencies>())
        .Compile();

    public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
        = CreateLambda<SelectExpression, IDictionary<ProjectionMember, Expression>>(
            param => param.AccessField("_projectionMapping"))
        .Compile();

    public static readonly Action<QuerySqlGenerator> InitQuerySqlGenerator
        = S_InitQuerySqlGenerator();

    public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
        = S_ApplyPredicate();

    public static readonly Action<SelectExpression, string> ApplyAlias
        = S_ApplyAlias();
}
