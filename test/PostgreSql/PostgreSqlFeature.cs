using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using System;

internal static partial class ContextUtil
{
    public const string ConvertLenContentToInt = "length(\"Content\")";

    public static DbContextOptions<TContext> GetOptions2<TContext>() where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        optionsBuilder.UseNpgsql(
            $"User ID=postgres;" +
            $"Password=Password12!;" +
            $"Host=localhost;" +
            $"Port=5432;" +
            $"Database=mengqinyu;" +
            $"Pooling=true;",
            s => s.UseBulk());

        optionsBuilder.UseTableSplittingJoinsRemoval();

        optionsBuilder.UseLoggerFactory(
            LoggerFactory.Create(builder => builder
                .AddDebug()
                .AddConsole()));

        return optionsBuilder.Options;
    }

    public static void EnsureContext(this DbContext context)
    {
        if (!context.Database.EnsureCreated())
        {
            var script = context.Database.GenerateCreateScript();
            foreach (var line in script.Trim().Split(";\r\n\r\n", StringSplitOptions.RemoveEmptyEntries))
                context.Database.ExecuteSqlRaw(line.Trim());
        }
    }

    public static void DropContext(this DbContext context)
    {
        foreach (var item in context.Model.GetEntityTypes())
        {
            var tableName = item.GetTableName();
            if (tableName != null)
                context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS \"{tableName}\"");
        }
    }

#if EFCORE31
    public static PropertyBuilder<TProperty> HasComputedColumnSql<TProperty>(this PropertyBuilder<TProperty> _, string __, bool ___)
    {
        return _.HasComputedColumnSql(__);
    }
#endif
}