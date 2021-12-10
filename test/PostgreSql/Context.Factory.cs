using System;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override string ScriptSplit => ";\r\n\r\n";

        protected override string DropTableCommand => "DROP TABLE IF EXISTS \"{0}\"";

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            string username = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
            string password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "Password12!";

            optionsBuilder.UseNpgsql(
                $"User ID={username};" +
                $"Password={password};" +
                $"Host=localhost;" +
                $"Port=5432;" +
                $"Database=efcorebulktest{Suffix};" +
                $"Pooling=true;",
                s => s.UseBulk().UseLegacyDateTimeOffset());
        }
    }
}
