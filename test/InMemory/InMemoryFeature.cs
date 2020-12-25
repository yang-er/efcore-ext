using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

internal static partial class ContextUtil
{
    public const string ConvertLenContentToInt = "";

    public static DbContextOptions<TContext> GetOptions2<TContext>() where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        optionsBuilder.UseInMemoryDatabase(
            "Nothing",
            s => s.UseBulk());

        optionsBuilder.UseLoggerFactory(
            LoggerFactory.Create(builder => builder
                .AddDebug()
                .AddConsole()));

        return optionsBuilder.Options;
    }

    public static void EnsureContext(this DbContext context)
    {
    }

    public static void DropContext(this DbContext context)
    {
    }

    public static void ToTable<TEntity>(this EntityTypeBuilder<TEntity> _, string __)
        where TEntity : class
    {
    }

    public static PropertyBuilder<TProperty> HasComputedColumnSql<TProperty>(this PropertyBuilder<TProperty> _, string __, bool ___)
    {
        return _;
    }

    public static IQueryable<T> FromSqlRaw<T>(this DbSet<T> _, string __) where T : class
    {
        throw new NotSupportedException("Non-relational doesn't support form sql.");
    }
}