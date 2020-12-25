using Microsoft.EntityFrameworkCore;
using System;
using Xunit;

public sealed class PrepareDatabase : IDisposable
{
    readonly Func<PrepareContext> factory;

    private class HelloWorld
    {
        public int Id { get; set; }
        public string Description { get; set; }
    }

    private class PrepareContext : DbContext
    {
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

    public PrepareDatabase()
    {
        factory = ContextUtil.MakeContextFactory<PrepareContext>();
        using var context = factory();
        context.EnsureContext();
    }

    public void Dispose()
    {
        using var context = factory();
        context.Database.EnsureDeleted();
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<PrepareDatabase>
{
}