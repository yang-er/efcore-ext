using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class PrepareContext : DbContext
    {
        public class HelloWorld
        {
            public int Id { get; set; }
            public string Description { get; set; }
        }

        public string DefaultSchema { get; }

        public PrepareContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HelloWorld>(entity =>
            {
                entity.ToTable(nameof(HelloWorld) + "_" + DefaultSchema);
            });
        }
    }

    [CollectionDefinition("DatabaseCollection")]
    public abstract class DatabaseCollection<TFactory> :
        ICollectionFixture<TFactory>,
        IDisposable
        where TFactory : class, IDbContextFactory<PrepareContext>
    {
        private readonly TFactory _factory;

        protected DatabaseCollection(TFactory factory)
        {
            _factory = factory;
            using var context = _factory.Create();
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            using var context = _factory.Create();
            context.Database.EnsureDeleted();
        }
    }
}
