using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;

internal static partial class ContextUtil
{
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

    public static PropertyBuilder<TProperty> HasComputedColumnSql<TProperty>(this PropertyBuilder<TProperty> _, string __)
    {
        return _;
    }
}