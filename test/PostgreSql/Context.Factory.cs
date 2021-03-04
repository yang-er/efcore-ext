using System;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(
                $"User ID=postgres;" +
                $"Password=Password12!;" +
                $"Host=localhost;" +
                $"Port=5432;" +
                $"Database=mengqinyu;" +
                $"Pooling=true;",
                s => s.UseBulk());
        }

        protected override void EnsureCreated(TContext context)
        {
            if (!context.Database.EnsureCreated())
            {
                var script = context.Database.GenerateCreateScript();
                foreach (var line in script.Trim().Split(";\r\n\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    context.Database.ExecuteSqlRaw(line.Trim());
                }
            }
        }

        protected override void EnsureDeleted(TContext context)
        {
            foreach (var item in context.Model.GetEntityTypes())
            {
                var tableName = item.GetTableName();
                if (tableName != null)
                {
                    context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS \"{tableName}\"");
                }
            }
        }
    }
}
