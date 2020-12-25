using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Testcase_BatchDelete
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

    public class DeleteContext : DbContext
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
    }

    public sealed class NameFixture : IDisposable
    {
        public string Schema { get; }

        public Func<DeleteContext> ContextFactory { get; }

        public List<Item> Items { get; }

        public NameFixture()
        {
            Schema = Guid.NewGuid().ToString()[0..6];
            ContextFactory = ContextUtil.MakeContextFactory<DeleteContext>();
            using var context = ContextFactory();
            context.EnsureContext();

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

            context.Items.AddRange(entities);
            context.SaveChanges();
            Items = entities;
        }

        public void Dispose()
        {
            using var context = ContextFactory();
            context.DropContext();
        }
    }

    [Collection("DatabaseCollection")]
    [TestCaseOrderer("PriorityOrderer", "EFCore.BulkExtensions.Tests")]
    public class DeleteSql : IClassFixture<NameFixture>
    {
        readonly Func<DeleteContext> contextFactory;

        readonly List<Item> items;

        public DeleteSql(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
            items = nameFixture.Items;
        }

        [Theory, TestPriority(0)]
        [InlineData(1)]
        public void WithTop(int takeCount)
        {
            using var context = contextFactory();
            var count = context.Items
                .Where(a => a.ItemId > 500)
                .Take(takeCount)
                .BatchDelete();
            items.Remove(items.First(a => a.ItemId > 500));
            Assert.Equal(takeCount, count);
        }

        [Fact, TestPriority(1)]
        public void ConstantCondition()
        {
            using var context = contextFactory();
            var it2 = items.Where(a => a.ItemId > 500 && a.Price == 3).ToList();
            var count = context.Items
                .Where(a => a.ItemId > 500 && a.Price == 3)
                .BatchDelete();
            Assert.Equal(it2.Count, count);
            it2.ForEach(a => items.Remove(a));
        }

        [Fact, TestPriority(2)]
        public void ParameteredCondition()
        {
            using var context = contextFactory();
            var nameToDelete = "N4";
            context.Items.Where(a => a.Name == nameToDelete).BatchDelete();
        }

        [Fact, TestPriority(3)]
        public void ContainsSomething()
        {
            using var context = contextFactory();
            var descriptionsToDelete = new List<string> { "info", "aaa" };
            context.Items.Where(a => descriptionsToDelete.Contains(a.Description)).BatchDelete();
        }

        [Fact, TestPriority(4)]
        public void ContainsAndAlsoEqual()
        {
            using var context = contextFactory();
            var descriptionsToDelete = new List<string> { "info" };
            var nameToDelete = "N4";
            context.Items
                .Where(a => descriptionsToDelete.Contains(a.Description) || a.Name == nameToDelete)
                .BatchDelete();
        }

        [Fact, TestPriority(5)]
        public void EmptyContains()
        {
            using var context = contextFactory();
            var descriptionsToDelete = new List<string>();
            context.Items.Where(a => descriptionsToDelete.Contains(a.Description)).BatchDelete();
        }

        [Fact, TestPriority(6)]
        public void ListAny()
        {
            using var context = contextFactory();
            var descriptionsToDelete = new List<string> { "info" };
            context.Items.Where(a => descriptionsToDelete.Any(toDelete => toDelete == a.Description)).BatchDelete();
        }
    }
}
