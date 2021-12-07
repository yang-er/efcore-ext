namespace Microsoft.EntityFrameworkCore.Tests
{
    public class NpgsqlContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override string ScriptSplit => ";\r\n\r\n";

        protected override string DropTableCommand => "DROP TABLE IF EXISTS \"{0}\"";

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(
                $"User ID=postgres;" +
                $"Password=Password12!;" +
                $"Host=localhost;" +
                $"Port=5432;" +
                $"Database=mengqinyu;" +
                $"Pooling=true;",
                s => s.UseBulk().UseLegacyDateTimeOffset());
        }
    }
}
