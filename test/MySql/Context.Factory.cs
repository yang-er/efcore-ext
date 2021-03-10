using System;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString =
                $"Server=localhost;" +
                $"Port=3306;" +
                $"Database=efcorebulktest;" +
                $"User=root;" +
                $"Password=Password12!;" +
                $"Character Set=utf8;" +
                $"TreatTinyAsBoolean=true;";

            optionsBuilder.UseMySql(
                connectionString,
#if EFCORE50
                ServerVersion.AutoDetect(connectionString),
#endif
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
                    context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS `{tableName}`");
                }
            }
        }
    }
}
