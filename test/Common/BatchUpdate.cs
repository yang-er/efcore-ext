using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
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

    public class Detail
    {
        public int Id { get; set; }

        public int Another { get; set; }

        public Judging Judging { get; set; }

        public int JudgingId { get; set; }
    }

    public class UpdateContext : DbContext
    {
        public DbSet<Item> Items { get; set; }
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<Judging> Judgings { get; set; }
        public DbSet<Detail> Details { get; set; }

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

            modelBuilder.Entity<Detail>(entity =>
            {
                entity.ToTable(nameof(Detail) + "_" + DefaultSchema);

                entity.HasOne<Judging>(e => e.Judging)
                    .WithMany()
                    .HasForeignKey(e => e.JudgingId);
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

        [ConditionalFact, TestPriority(-1)]
        public void ConstantUpdateBody()
        {
            using var context = contextFactory();
            decimal price = 0;

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .BatchUpdate(a => new Item { Description = "Updated", Price = 1.5m });

            int t = 0;

            foreach (var o in items.Where(a => a.ItemId <= 388 && a.Price >= price))
            {
                o.Description = "Updated";
                o.Price = 1.5m;
                t++;
            }

            Assert.Equal(t, state);

            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, decimal price)
                    => ctx.Items
                        .Where(a => a.ItemId <= 388 && a.Price >= price)
                        .BatchUpdate(a => new Item { Description = "Updated", Price = 1.5m }));
            compiledQuery(context, price);
            compiledQuery(context, price);
        }

        [ConditionalFact, TestPriority(0)]
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

            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, decimal price, string desc, decimal pri)
                    => ctx.Items
                        .Where(a => a.ItemId <= 388 && a.Price >= price)
                        .BatchUpdate(a => new Item { Description = desc, Price = pri }));
            compiledQuery(context, price, "Aaa", 3m);
            compiledQuery(context, price, "bbb", 2m);
        }

        [ConditionalFact, TestPriority(1)]
        public void WithTakeTopSkip_MustFail()
        {
            using var context = contextFactory();
            decimal price = 0;

            Assert.Throws<InvalidOperationException>(
                () => context.Items
                    .Where(a => a.ItemId <= 388 && a.Price >= price)
                    .Take(10)
                    .BatchUpdate(a => new Item { Price = a.Price == 1.5m ? 3.0m : price, Description = a.Description + " TOP(10)" }));

            Assert.Throws<InvalidOperationException>(
                () => context.Items
                    .Where(a => a.ItemId <= 388 && a.Price >= price)
                    .Skip(10)
                    .BatchUpdate(a => new Item { Price = a.Price == 1.5m ? 3.0m : price, Description = a.Description + " TOP(10)" }));
        }

        [ConditionalFact, TestPriority(2)]
        [DatabaseProviderSkipCondition(DatabaseProvider.PostgreSQL, SkipVersion = EFCoreVersion.Version_3_1)]
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

        [ConditionalFact, TestPriority(3)]
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

        [ConditionalFact, TestPriority(4)]
        public void SetNull()
        {
            using var context = contextFactory();
            context.Judgings
                .BatchUpdate(a => new Judging
                {
                    CompileError = null,
                    ExecuteMemory = null,
                    PreviousJudgingId = null,
                    TotalScore = null,
                    StartTime = DateTimeOffset.Now,
                    Server = null,
                    Status = Math.Max(a.Status, 8),
                });
        }

        [ConditionalFact, TestPriority(5)]
        public void NavigationWhere()
        {
            using var context = contextFactory();

            int x = 10;
            context.Details
                .Where(a => a.Judging.PreviousJudgingId == x)
                .BatchUpdate(a => new Detail { Another = a.Judging.SubmissionId, });

            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, int x)
                    => ctx.Details
                        .Where(a => a.Judging.PreviousJudgingId == x)
                        .BatchUpdate(a => new Detail { Another = a.Judging.SubmissionId }));
            compiledQuery(context, 5);
            compiledQuery(context, 7);
        }

        [ConditionalFact, TestPriority(6)]
        public void NavigationSelect()
        {
            using var context = contextFactory();

            int x = 10;
            context.Details.BatchUpdate(a => new Detail { Another = a.Another + a.Judging.SubmissionId + x, });

            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, int x)
                    => ctx.Details
                        .BatchUpdate(a => new Detail { Another = a.Another + a.Judging.SubmissionId + x, }));
            compiledQuery(context, 5);
            compiledQuery(context, 7);
        }
    }
}
