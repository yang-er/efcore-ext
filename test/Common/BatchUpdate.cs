using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Testcase_BatchUpdate
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

    public class ChangeLog
    {
        public int ChangeLogId { get; set; }
        public string Description { get; set; }
        public Audit Audit { get; set; }
    }

    [Owned] public class Audit
    {
        [Column(nameof(ChangedBy))] public string ChangedBy { get; set; }
        public bool IsDeleted { get; set; }
        [NotMapped] public DateTime? ChangedTime { get; set; }
    }

    public class Judging
    {
        public int JudgingId { get; set; }
        public bool Active { get; set; }
        public int SubmissionId { get; set; }
        public bool FullTest { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? StopTime { get; set; }
        public string Server { get; set; }
        public int Status { get; set; }
        public int? ExecuteTime { get; set; }
        public int? ExecuteMemory { get; set; }
        public string CompileError { get; set; }
        public int? RejudgeId { get; set; }
        public int? PreviousJudgingId { get; set; }
        public int? TotalScore { get; set; }
    }

    public class UpdateContext : DbContext
    {
        public DbSet<Item> Items { get; set; }
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<Judging> Judgings { get; set; }

        public string DefaultSchema { get; }

        public UpdateContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeLog>(entity =>
            {
                entity.ToTable(nameof(ChangeLog) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<Judging>(entity =>
            {
                entity.ToTable(nameof(Judging) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable(nameof(Item) + "_" + DefaultSchema);
            });
        }
    }

    public sealed class NameFixture : IDisposable
    {
        public string Schema { get; }

        public Func<UpdateContext> ContextFactory { get; }

        public List<Item> Items { get; }

        public NameFixture()
        {
            Schema = Guid.NewGuid().ToString()[0..6];
            ContextFactory = ContextUtil.MakeContextFactory<UpdateContext>();
            using var context = ContextFactory();
            context.EnsureContext();

            var entities = new List<Item>();
            for (int i = 1; i <= 500; i++)
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
    public class UpdateSql : IClassFixture<NameFixture>
    {
        readonly Func<UpdateContext> contextFactory;
        readonly List<Item> items;

        public UpdateSql(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
            items = nameFixture.Items;
        }

        [Fact, TestPriority(-1)]
        public void ConstantUpdateBody()
        {
            using var context = contextFactory();
            decimal price = 0;

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .BatchUpdate(a => new Item { Description = "Updated", Price = 1.5m }/*, updateColumns*/);

            int t = 0;

            foreach (var o in items.Where(a => a.ItemId <= 388 && a.Price >= price))
            {
                o.Description = "Updated";
                o.Price = 1.5m;
                t++;
            }

            Assert.Equal(t, state);
        }

        [Fact, TestPriority(0)]
        public void ParameterUpdateBody()
        {
            using var context = contextFactory();
            decimal price = 0;
            var desc = "Updated";
            var pri = 1.5m;

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .BatchUpdate(a => new Item { Description = desc, Price = pri });

            int t = 0;

            foreach (var o in items.Where(a => a.ItemId <= 388 && a.Price >= price))
            {
                o.Description = desc;
                o.Price = pri;
                t++;
            }

            Assert.Equal(t, state);
        }

        [Fact, TestPriority(1)]
        public void WithTakeTop()
        {
            using var context = contextFactory();
            decimal price = 0;

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .Take(10)
                .BatchUpdate(a => new Item { Price = a.Price == 1.5m ? 3.0m : price, Description = a.Description + " TOP(10)" });

            int t = 0;

            foreach (var o in items.Where(a => a.ItemId <= 388 && a.Price >= price).Take(10))
            {
                o.Price = o.Price == 1.5m ? 3.0m : price;
                o.Description += " TOP(10)";
                t++;
            }

            Assert.Equal(t, state);
        }

        [Fact, TestPriority(2)]
        public void HasOwnedType()
        {
            using var context = contextFactory();

            context.ChangeLogs.BatchUpdate(c => new ChangeLog
            {
                Audit = new Audit
                {
                    IsDeleted = !c.Audit.IsDeleted
                }
            });
        }

        [Fact, TestPriority(3)]
        public void ConcatenateBody()
        {
            using var context = contextFactory();
            decimal price = 0;
            var incrementStep = 100;
            var suffix = " Concatenated";

            var query = context.Items
                .Where(a => a.ItemId <= 500 && a.Price >= price)
                .BatchUpdate(a => new Item { Name = a.Name + suffix, Quantity = a.Quantity + incrementStep });

            int t = 0;

            foreach (var o in items.Where(a => a.ItemId <= 500 && a.Price >= price))
            {
                o.Name += suffix;
                o.Quantity += incrementStep;
                t++;
            }

            Assert.Equal(t, query);
        }

        [Fact, TestPriority(4)]
        public void SetNull()
        {
            using var context = contextFactory();
            context.Judgings
                .Take(100000)
                .BatchUpdate(a => new Judging
                {
                    CompileError = null,
                    ExecuteMemory = null,
                    PreviousJudgingId = null,
                    TotalScore = null,
                    StartTime = DateTimeOffset.Now,
                    Server = null,
                });
        }
    }
}
