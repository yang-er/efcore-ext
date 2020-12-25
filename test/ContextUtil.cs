using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Linq.Expressions;

internal static class ContextUtil
{
    public static DbContextOptions<TContext> GetOptions2<TContext>() where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        optionsBuilder.UseSqlServer(
            $"Server=(localdb)\\mssqllocaldb;" +
            $"Database=EFCoreBulkTest;" +
            $"Trusted_Connection=True;" +
            $"MultipleActiveResultSets=true");

        optionsBuilder.UseBulkExtensions();

        optionsBuilder.UseLoggerFactory(
            LoggerFactory.Create(builder => builder
                .AddDebug()
                .AddConsole(c => c.DisableColors = true)));

        return optionsBuilder.Options;
    }

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

    public static void EnsureContext(this DbContext context)
    {
        if (!context.Database.EnsureCreated())
        {
            var script = context.Database.GenerateCreateScript();
            foreach (var line in script.Trim().Split("\r\nGO", StringSplitOptions.RemoveEmptyEntries))
                context.Database.ExecuteSqlRaw(line.Trim());
        }
    }

    public static void DropContext(this DbContext context)
    {
        foreach (var item in context.Model.GetEntityTypes())
        {
            var tableName = item.GetTableName();
            if (tableName != null)
                context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS [{tableName}]");
        }
    }
}