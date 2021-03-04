using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerContextFactory<TContext> :
        RelationalContextFactoryBase<TContext>,
        IClassFixture<ContextLoggerFactory>
        where TContext : DbContext
    {
        public SqlServerContextFactory(ContextLoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                $"Server=(localdb)\\mssqllocaldb;" +
                $"Database=EFCoreBulkTest;" +
                $"Trusted_Connection=True;" +
                $"MultipleActiveResultSets=true",
                s => s.UseBulk());
        }

        protected override void EnsureCreated(TContext context)
        {
            if (!context.Database.EnsureCreated())
            {
                var script = context.Database.GenerateCreateScript();
                foreach (var line in script.Trim().Split("\r\nGO", StringSplitOptions.RemoveEmptyEntries))
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
                    context.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS [{tableName}]");
                }
            }
        }
    }
}
