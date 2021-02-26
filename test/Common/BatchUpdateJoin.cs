using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Xunit;

namespace Testcase_BatchUpdateJoin
{
    public class ItemA
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    public class ItemB
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    public class UpdateContext : DbContext
    {
        public DbSet<ItemA> A { get; set; }
        public DbSet<ItemB> B { get; set; }

        public string DefaultSchema { get; }

        public UpdateContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ItemA>(entity =>
            {
                entity.Property(a => a.Id).ValueGeneratedNever();
                entity.ToTable(nameof(ItemA) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<ItemB>(entity =>
            {
                entity.Property(a => a.Id).ValueGeneratedNever();
                entity.ToTable(nameof(ItemB) + "_" + DefaultSchema);
            });
        }
    }

    public sealed class NameFixture : IDisposable
    {
        public string Schema { get; }

        public Func<UpdateContext> ContextFactory { get; }

        public NameFixture()
        {
            Schema = Guid.NewGuid().ToString()[0..6];
            ContextFactory = ContextUtil.MakeContextFactory<UpdateContext>();
            using var context = ContextFactory();
            context.EnsureContext();
            context.A.Add(new ItemA { Id = 1, Value = 1 });
            context.B.Add(new ItemB { Id = 1, Value = 2 });
            context.A.Add(new ItemA { Id = 2, Value = 1 });
            context.B.Add(new ItemB { Id = 2, Value = 2 });
            context.SaveChanges();
        }

        public void Dispose()
        {
            using var context = ContextFactory();
            context.DropContext();
        }
    }

    [Collection("DatabaseCollection")]
    public class UpdateJoinSql : IClassFixture<NameFixture>
    {
        readonly Func<UpdateContext> contextFactory;

        public UpdateJoinSql(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
        }

        [ConditionalFact, TestPriority(-1)]
        public void NormalUpdate()
        {
            using var context = contextFactory();

            context.A.BatchUpdateJoin(
                inner: context.B,
                outerKeySelector: a => a.Id,
                innerKeySelector: b => b.Id,
                condition: (a, b) => a.Id == 1,
                updateSelector: (a, b) => new ItemA { Value = a.Value + b.Value - 3 });

            var list = context.A.OrderBy(a => a.Id).ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0].Id);
            Assert.Equal(0, list[0].Value);
            Assert.Equal(2, list[1].Id);
            Assert.Equal(1, list[1].Value);
        }

        [ConditionalFact, TestPriority(0)]
        public void LocalTableJoin()
        {
            using var context = contextFactory();

            var lst = new[]
            {
                new { Id = 1, Value = 3 },
                new { Id = 2, Value = 4 },
                new { Id = 3, Value = 5 },
            };

            context.B.BatchUpdateJoin(
                inner: lst,
                outerKeySelector: a => a.Id,
                innerKeySelector: b => b.Id,
                condition: (a, b) => a.Id != 2,
                updateSelector: (a, b) => new ItemB { Value = a.Value + b.Value });

            var list = context.B.OrderBy(a => a.Id).ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0].Id);
            Assert.Equal(5, list[0].Value);
            Assert.Equal(2, list[1].Id);
            Assert.Equal(2, list[1].Value);
        }
    }
}
