using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cacheable;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Testcase_Cacheable
{
    public class Item
    {
        public int Id { get; set; }
    }

    public class DefaultContext : DbContext
    {
        public DbSet<Item> Items { get; set; }

        public DefaultContext(DbContextOptions options) : base(options)
        {
        }
    }

    public sealed class NameFixture : IDisposable
    {
        public Func<DefaultContext> ContextFactory { get; }

        private void Confiugre(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("Nothing2");
            options.UseCache();
        }

        public NameFixture()
        {
            var options = new DbContextOptionsBuilder<DefaultContext>();
            Confiugre(options);
            ContextFactory = () => new DefaultContext(options.Options);

            using var context = ContextFactory();
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            using var context = ContextFactory();
            context.Database.EnsureDeleted();
        }
    }

    public class CacheableFunctional : IClassFixture<NameFixture>
    {
        private readonly Func<DefaultContext> _contextFactory;

        public CacheableFunctional(NameFixture fixture)
        {
            _contextFactory = fixture.ContextFactory;
        }

        [ConditionalFact]
        public async Task EnsureUseable()
        {
            using (var ctx = _contextFactory())
            {
                Assert.Equal(0, await ctx.Items.CachedCountAsync("1", TimeSpan.FromDays(10)));

                ctx.Items.Add(new Item());
                await ctx.SaveChangesAsync();

                Assert.Equal(0, await ctx.Items.CachedCountAsync("1", TimeSpan.FromDays(10)));
                Assert.Equal(1, await ctx.Items.CachedCountAsync("2", TimeSpan.FromDays(10)));
            }

            using (var ctx = _contextFactory())
            {
                ctx.Items.Add(new Item());
                await ctx.SaveChangesAsync();

                Assert.Equal(0, await ctx.Items.CachedCountAsync("1", TimeSpan.FromDays(10)));
                Assert.Equal(1, await ctx.Items.CachedCountAsync("2", TimeSpan.FromDays(10)));
                Assert.Equal(2, await ctx.Items.CachedCountAsync("3", TimeSpan.FromDays(10)));
            }
        }
    }
}
