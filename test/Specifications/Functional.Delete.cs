using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.BatchDelete
{
    public class Item
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public DateTimeOffset TimeUpdated { get; set; }
    }

    public class DeleteContext : DbContext, IDbContextWithSeeds
    {
        public DbSet<Item> Items { get; set; }
        public string DefaultSchema { get; }

        public DeleteContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable(nameof(Item) + "_" + DefaultSchema);
            });
        }

        public object Seed()
        {
            var entities = new List<Item>();
            for (int i = 1; i <= 1000; i++)
            {
                var entity = new Item
                {
                    Name = "name " + Guid.NewGuid().ToString().Substring(0, 3),
                    Description = "info",
                    Quantity = i % 10,
                    Price = i / (i % 5 + 1),
                    TimeUpdated = DateTime.Now,
                };

                entities.Add(entity);
            }

            Items.AddRange(entities);
            SaveChanges();
            return entities;
        }
    }

    public abstract class DeleteTestBase<TFactory> : QueryTestBase<DeleteContext, TFactory>
        where TFactory : class, IDbContextFactory<DeleteContext>
    {
        private readonly List<Item> Items;

        protected DeleteTestBase(TFactory factory) : base(factory)
        {
            Items = (List<Item>)factory.Seed;
        }

        [ConditionalFact, TestPriority(0)]
        public void WithTop_MustFail()
        {
            using var context = CreateContext();

            Assert.Throws<InvalidOperationException>(
                () => context.Items
                    .Where(a => a.ItemId > 500)
                    .Take(10)
                    .BatchDelete());

            Assert.Throws<InvalidOperationException>(
                () => context.Items
                    .Where(a => a.ItemId > 500)
                    .Skip(10)
                    .BatchDelete());
        }

        [ConditionalFact, TestPriority(1)]
        public void ConstantCondition()
        {
            var it2 = Items.Where(a => a.ItemId > 500 && a.Price == 124).ToList();
            it2.ForEach(a => Items.Remove(a));

            using (CatchCommand())
            {
                using var context = CreateContext();

                var count = context.Items
                    .Where(a => a.ItemId > 500 && a.Price == 124)
                    .BatchDelete();

                Assert.Equal(it2.Count, count);
            }
        }

        [ConditionalFact, TestPriority(2)]
        public void ParameteredCondition()
        {
            var nameToDelete = "N4";

            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Items.Where(a => a.Name == nameToDelete).BatchDelete();
        }

        [ConditionalFact, TestPriority(3)]
        public void ContainsSomething()
        {
            var descriptionsToDelete = new List<string> { "info", "aaa" };

            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Items.Where(a => descriptionsToDelete.Contains(a.Description)).BatchDelete();
        }

        [ConditionalFact, TestPriority(4)]
        public void ContainsAndAlsoEqual()
        {
            var descriptionsToDelete = new List<string> { "info" };
            var nameToDelete = "N4";
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.Items
                .Where(a => descriptionsToDelete.Contains(a.Description) || a.Name == nameToDelete)
                .BatchDelete();
        }

        [ConditionalFact, TestPriority(5)]
        public void EmptyContains()
        {
            var descriptionsToDelete = new List<string>();
            using var context = CreateContext();
            using var scope = CatchCommand();
            context.Items.Where(a => descriptionsToDelete.Contains(a.Description)).BatchDelete();
        }

        [ConditionalFact, TestPriority(6)]
        public void ListAny()
        {
            var descriptionsToDelete = new List<string> { "info" };
            using var context = CreateContext();
            using var scope = CatchCommand();
            context.Items.Where(a => descriptionsToDelete.Any(toDelete => toDelete == a.Description)).BatchDelete();
        }

        [ConditionalFact, TestPriority(7)]
        public void CompiledQuery_ConstantCondition()
        {
            var compiledQuery = EF.CompileQuery(
                (DeleteContext ctx) =>
                    ctx.Items
                        .Where(a => a.ItemId > 500 && a.Price == 3)
                        .BatchDelete());

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context);
            }
        }

        [ConditionalFact, TestPriority(8)]
        public void CompiledQuery_ParameteredCondition()
        {
            var compiledQuery = EF.CompileQuery(
                (DeleteContext ctx, string nameToDelete) =>
                    ctx.Items.Where(a => a.Name == nameToDelete).BatchDelete());

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context, "N4");
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context, "N5");
            }
        }

        [ConditionalFact, TestPriority(9)]
        public void CompiledQuery_ContainsSomething()
        {
            var compiledQuery = EF.CompileQuery(
                (DeleteContext ctx, List<string> descriptionsToDelete) =>
                    ctx.Items.Where(a => descriptionsToDelete.Contains(a.Name)).BatchDelete());

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context, new List<string> { "info", "aaa" });
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context, new List<string> { "jyntnytjyntjntnytnt", "aaa" });
            }
        }
    }
}
