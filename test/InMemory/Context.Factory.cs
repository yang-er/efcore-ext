namespace Microsoft.EntityFrameworkCore.Tests
{
    public class InMemoryContextFactory<TContext> : ContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(
                "Nothing",
                s => s.UseBulk());
        }

        protected override void EnsureCreated(TContext context)
        {
        }

        protected override void EnsureDeleted(TContext context)
        {
        }
    }
}
