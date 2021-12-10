namespace Microsoft.EntityFrameworkCore.Tests
{
    public class SqlServerContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override string ScriptSplit => "\r\nGO";

        protected override string DropTableCommand => "DROP TABLE IF EXISTS [{0}]";

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                $"Server=(localdb)\\mssqllocaldb;" +
                $"Database=efcorebulktest{Suffix};" +
                $"Trusted_Connection=True;" +
                $"MultipleActiveResultSets=true",
                s => s.UseBulk().UseMathExtensions());
        }
    }
}
