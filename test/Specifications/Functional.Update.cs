﻿using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.BatchUpdate
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

    public class UpdateContext : DbContext, IDbContextWithSeeds
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

        public object Seed()
        {
            var entities = new List<Item>();
            for (int i = 1; i <= 500; i++)
            {
                entities.Add(new Item
                {
                    Name = "name " + Guid.NewGuid().ToString().Substring(0, 3),
                    Description = "info",
                    Quantity = i % 10,
                    Price = i / (i % 5 + 1),
                    TimeUpdated = DateTime.Now,
                });
            }

            Items.AddRange(entities);
            Details.Add(new Detail { Judging = new Judging { } });
            SaveChanges();
            return entities;
        }
    }

    public abstract class UpdateTestBase<TFactory> : QueryTestBase<UpdateContext, TFactory>
        where TFactory : class, IDbContextFactory<UpdateContext>
    {
        private readonly List<Item> Items;

        public UpdateTestBase(TFactory factory) : base(factory)
        {
            Items = (List<Item>)factory.Seed;
        }

        [ConditionalFact, TestPriority(-1)]
        public void ConstantUpdateBody()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();
            decimal price = 0;

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .BatchUpdate(a => new Item { Description = "Updated", Price = 1.5m });

            int t = 0;

            foreach (var o in Items.Where(a => a.ItemId <= 388 && a.Price >= price))
            {
                o.Description = "Updated";
                o.Price = 1.5m;
                t++;
            }

            Assert.Equal(t, state);
        }

        [ConditionalFact, TestPriority(0)]
        public void ParameterUpdateBody()
        {
            decimal price = 0;
            var desc = "Updated";
            var pri = 1.5m;
            using var scope = CatchCommand();
            using var context = CreateContext();

            var state = context.Items
                .Where(a => a.ItemId <= 388 && a.Price >= price)
                .BatchUpdate(a => new Item { Description = desc, Price = pri });

            int t = 0;

            foreach (var o in Items.Where(a => a.ItemId <= 388 && a.Price >= price))
            {
                o.Description = desc;
                o.Price = pri;
                t++;
            }

            Assert.Equal(t, state);
        }

        [ConditionalFact, TestPriority(1)]
        public void WithTakeTopSkip_MustFail()
        {
            using var context = CreateContext();
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
        [DatabaseProviderSkipCondition(DatabaseProvider.PostgreSQL_31)]
        public void HasOwnedType()
        {
            using (var initctx = CreateContext())
            {
                initctx.ChangeLogs.Add(new ChangeLog
                {
                    Audit = new Audit
                    {
                        IsDeleted = false,
                        ChangedBy = "wdwdw",
                        ChangedTime = DateTime.Now,
                    },
                    Description = "wfefvrg",
                });

                initctx.SaveChanges();
            }

            using (CatchCommand())
            using (var context = CreateContext())
            {
                context.ChangeLogs.BatchUpdate(c => new ChangeLog
                {
                    Audit = new Audit
                    {
                        IsDeleted = !c.Audit.IsDeleted
                    }
                });
            }

            using (var context = CreateContext())
            {
                var chg = context.ChangeLogs.Single();
                Assert.Equal("wdwdw", chg.Audit.ChangedBy);
                Assert.True(chg.Audit.IsDeleted);
            }
        }

        [ConditionalFact, TestPriority(3)]
        public void ConcatenateBody()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();

            decimal price = 0;
            var incrementStep = 100;
            var suffix = " Concatenated";

            var query = context.Items
                .Where(a => a.ItemId <= 500 && a.Price >= price)
                .BatchUpdate(a => new Item { Name = a.Name + suffix, Quantity = a.Quantity + incrementStep });

            int t = 0;

            foreach (var o in Items.Where(a => a.ItemId <= 500 && a.Price >= price))
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
            using var scope = CatchCommand();
            using var context = CreateContext();

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
            int x = 10;
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Details
                .Where(a => a.Judging.PreviousJudgingId == x)
                .BatchUpdate(a => new Detail { Another = a.Judging.SubmissionId });
        }

        [ConditionalFact, TestPriority(6)]
        public void NavigationSelect()
        {
            int x = 10;
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Details.BatchUpdate(a => new Detail { Another = a.Another + a.Judging.SubmissionId + x, });
        }

        [ConditionalFact, TestPriority(7)]
        public void ScalarSubquery()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Details.BatchUpdate(a => new Detail { Another = context.Items.Count() });
        }

        [ConditionalFact, TestPriority(8)]
        public void CompiledQuery_ConstantUpdateBody()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, decimal price)
                    => ctx.Items
                        .Where(a => a.ItemId <= 388 && a.Price >= price)
                        .BatchUpdate(a => new Item { Description = "Updated", Price = 1.5m }));


            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 1.0m);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 2.0m);
            }
        }

        [ConditionalFact, TestPriority(9)]
        public void CompiledQuery_ParameterUpdateBody()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, decimal price, string desc, decimal pri)
                    => ctx.Items
                        .Where(a => a.ItemId <= 388 && a.Price >= price)
                        .BatchUpdate(a => new Item { Description = desc, Price = pri }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 2.4m, "Aaa", 3m);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 1.8m, "bbb", 2m);
            }
        }

        [ConditionalFact, TestPriority(11)]
        [DatabaseProviderSkipCondition(DatabaseProvider.PostgreSQL_31)]
        public void CompiledQuery_HasOwnedType()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx) =>
                ctx.ChangeLogs.BatchUpdate(c => new ChangeLog
                {
                    Audit = new Audit
                    {
                        IsDeleted = !c.Audit.IsDeleted
                    }
                }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }
        }

        [ConditionalFact, TestPriority(12)]
        public void CompiledQuery_ConcatenateBody()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, decimal price, int incrementStep, string suffix)
                    => ctx.Items
                        .Where(a => a.ItemId <= 500 && a.Price >= price)
                        .BatchUpdate(a => new Item { Name = a.Name + suffix, Quantity = a.Quantity + incrementStep }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 0, 100, " Concatenated");
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 3.1m, 10, " hrt");
            }
        }

        [ConditionalFact, TestPriority(13)]
        public void CompiledQuery_SetNull()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx) =>
                    ctx.Judgings.BatchUpdate(a => new Judging
                    {
                        CompileError = null,
                        ExecuteMemory = null,
                        PreviousJudgingId = null,
                        TotalScore = null,
                        StartTime = DateTimeOffset.Now,
                        Server = null,
                        Status = Math.Max(a.Status, 8),
                    }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }
        }

        [ConditionalFact, TestPriority(14)]
        public void CompiledQuery_NavigationWhere()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, int x)
                    => ctx.Details
                        .Where(a => a.Judging.PreviousJudgingId == x)
                        .BatchUpdate(a => new Detail { Another = a.Judging.SubmissionId }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 5);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 7);
            }
        }

        [ConditionalFact, TestPriority(15)]
        public void CompiledQuery_NavigationSelect()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, int x)
                    => ctx.Details
                        .BatchUpdate(a => new Detail { Another = a.Another + a.Judging.SubmissionId + x }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 5);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 7);
            }
        }

        [ConditionalFact, TestPriority(16)]
        public void CompiledQuery_ScalarSubquery()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx)
                    => ctx.Details
                        .BatchUpdate(a => new Detail { Another = ctx.Items.Count() }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context);
            }
        }
    }
}
