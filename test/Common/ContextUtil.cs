using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;

internal static partial class ContextUtil
{
    public static Func<TContext> MakeContextFactory<TContext>() where TContext : DbContext
    {
        var schemaName = Guid.NewGuid().ToString()[0..6];
        var getOptions = Expression.Call(null,
            typeof(ContextUtil).GetMethod(nameof(GetOptions2)).MakeGenericMethod(typeof(TContext)));
        var getContext = Expression.New(
            typeof(TContext).GetConstructors().Single(),
            Expression.Constant(schemaName, typeof(string)),
            getOptions);
        return Expression.Lambda<Func<TContext>>(getContext).Compile();
    }
}