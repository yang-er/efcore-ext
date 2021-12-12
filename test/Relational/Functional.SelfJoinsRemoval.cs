using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.SelfJoinsRemoval
{
    public class ChangeLog
    {
        public int ChangeLogId { get; set; }
        public string Description { get; set; }
        public Audit Audit { get; set; }
    }

    public class MiniInfo
    {
        public int Id { get; }
        public int What { get; }
        public FullInfo Full { get; }
    }

    public class FullInfo
    {
        public int Id { get; }
        public int What { get; }
        public int Other { get; }
    }

    public class OwnedThree
    {
        public int Id { get; set; }
        public int Other { get; set; }
        public int? Happy { get; set; }
        public OwnedAndOwned Apple { get; set; }
        public OwnedAndOwnedAndOwned Banana { get; set; }
        public OwnedAndOwnedAndOwned Cherry { get; set; }
    }

    [Owned]
    public class OwnedAndOwnedAndOwned
    {
        public string HelloString { get; set; }
        public string Taq { get; }
    }

    [Owned]
    public class OwnedAndOwned
    {
        public OwnedAndOwnedAndOwned What { get; set; }
        public Audit Audit { get; set; }
        public string AnotherString { get; set; }
    }

    [Owned]
    public class Audit
    {
        [Column(nameof(ChangedBy))] public string ChangedBy { get; set; }
        public bool IsDeleted { get; set; }
        [NotMapped] public DateTime? ChangedTime { get; set; }
        public OwnedAndOwnedAndOwned What { get; set; }
    }

    public class NormalEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class AppleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
    }

    public class BananaEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string Source { get; set; }
    }

    public class TestingContext : DbContext
    {
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<MiniInfo> MiniInfos { get; set; }
        public DbSet<OwnedThree> OwnedThrees { get; set; }
        public DbSet<NormalEntity> NormalEntities { get; set; }
        public DbSet<AppleEntity> Apples { get; set; }
        public DbSet<BananaEntity> Bananas { get; set; }
        public string DefaultSchema { get; }

        public TestingContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseTableSplittingJoinsRemoval();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeLog>(entity =>
            {
                entity.ToTable(nameof(ChangeLog) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<OwnedThree>(entity =>
            {
                entity.ToTable(nameof(OwnedThree) + "_" + DefaultSchema);

                entity.OwnsOne(e => e.Banana, owned =>
                {
                    owned.Property(o => o.HelloString)
                        .HasColumnName("Banana_AnotherString");
                    owned.Property(o => o.Taq)
                        .HasColumnName("Banana_Taq");
                });

                entity.OwnsOne(e => e.Cherry, owned =>
                {
                    owned.Property(o => o.HelloString)
                        .HasColumnName("Cherry_AnotherString");
                    owned.Property(o => o.Taq)
                        .HasColumnName("Cherry_Taq");
                });

                entity.OwnsOne(e => e.Apple, owned =>
                {
                    owned.OwnsOne(o => o.What, owned =>
                    {
                        owned.Property(o => o.HelloString)
                            .HasColumnName("Apple_What_AnotherString");
                        owned.Property(o => o.Taq)
                            .HasColumnName("Banana_Taq");
                    });

                    owned.OwnsOne(o => o.Audit, owned =>
                    {
                        owned.OwnsOne(o => o.What, owned =>
                        {
                            owned.Property(o => o.HelloString)
                                .HasColumnName("Apple_What_AnotherString");
                            owned.Property(o => o.Taq)
                                .HasColumnName("Audit_Taq")
                                ;//.IsRequired();
                        });
                    });

                    owned.Property(e => e.AnotherString).IsRequired();
                });
            });

            modelBuilder.Entity<MiniInfo>(entity =>
            {
                entity.ToTable(nameof(MiniInfo) + "_" + DefaultSchema);

                entity.HasKey(e => e.Id);

                entity.Property(e => e.What)
                    .HasColumnName("What");

                entity.HasOne(e => e.Full)
                    .WithOne()
                    .HasForeignKey<FullInfo>(e => e.Id);
            });

            modelBuilder.Entity<FullInfo>(entity =>
            {
                entity.ToTable(nameof(MiniInfo) + "_" + DefaultSchema);

                entity.Property(e => e.What)
                    .HasColumnName("What");

                entity.Property(e => e.Other)
                    .HasColumnName("Other");

                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<NormalEntity>(entity =>
            {
                entity.ToTable(nameof(NormalEntity) + "_" + DefaultSchema);

                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<AppleEntity>(entity =>
            {
                entity.ToTable(nameof(AppleEntity) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<BananaEntity>(entity =>
            {
                entity.ToTable(nameof(BananaEntity) + "_" + DefaultSchema);
            });
        }
    }

    [DatabaseProviderSkipCondition(DatabaseProvider.InMemory)]
    public abstract class UselessJoinsRemovalTestBase<TFactory> : QueryTestBase<TestingContext, TFactory>
        where TFactory : class, IDbContextFactory<TestingContext>
    {
        protected UselessJoinsRemovalTestBase(TFactory factory)
            : base(factory)
        {
        }

        [ConditionalFact, TestPriority(0)]
        public virtual void Owned()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();
            context.ChangeLogs.TagWith("PruneSelfJoins").Load();
        }

        [ConditionalFact, TestPriority(1)]
        public virtual void HasOneWithOne_SharedTable()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();
            context.MiniInfos.Include(e => e.Full).TagWith("PruneSelfJoins").Load();
        }

        [ConditionalFact, TestPriority(2)]
        public virtual void SuperOwned()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();
            context.OwnedThrees.TagWith("PruneSelfJoins").Load();
        }

        [ConditionalFact, TestPriority(3)]
        public virtual void InnerJoin_3_2()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();
            
            context.OwnedThrees
                .Join(context.ChangeLogs, o => o.Id, o => o.ChangeLogId, (o, p) => new { o, p })
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(4)]
        public virtual void InnerJoin_2_3()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.ChangeLogs
                .Join(context.OwnedThrees, o => o.ChangeLogId, o => o.Id, (o, p) => new { o, p })
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(5)]
        public virtual void GroupJoin_3_2()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.OwnedThrees
                .GroupJoin(context.ChangeLogs, o => o.Id, o => o.ChangeLogId, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p })
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(6)]
        public virtual void GroupJoin_2_3()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.ChangeLogs
                .GroupJoin(context.OwnedThrees, o => o.ChangeLogId, o => o.Id, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p })
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(7)]
        public virtual void ReallyJoin()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.OwnedThrees
                .GroupJoin(context.OwnedThrees, o => o.Id, o => o.Happy, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p })
                .Join(context.OwnedThrees, o => o.o.Id, o => o.Other, (a, o) => new { a.o, a.p, o2 = o })
                .Where(o => o.o.Other != 3)
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(8)]
        public virtual void Owned_SkipTrimming()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.NormalEntities
                .Join(context.NormalEntities, a => a.Id, a => a.Id, (a, b) => a)
                .Load();
        }

        [ConditionalFact, TestPriority(9)]
        public virtual void ShaperChanged()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.NormalEntities
                .Where(a => a.Age > 80)
                .Union(
                    context.NormalEntities
                        .Where(a => a.Age < 20))
                .Union(
                    context.NormalEntities
                        .Where(a => a.Age == 50))
                .TagWith("hello")
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(10)]
        public virtual void ReallyUnionDistinct()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.NormalEntities
                .Where(a => a.Age > 80)
                .Select(n => new { n.Id, n.Name, n.Age, Type = 1 })
                .Union(
                    context.NormalEntities
                        .Where(a => a.Age < 20)
                        .Select(n => new { n.Id, n.Name, n.Age, Type = 2 }))
                .Union(
                    context.NormalEntities
                        .Where(a => a.Age == 50)
                        .Select(n => new { n.Id, n.Name, n.Age, Type = 3 }))
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(11)]
        public virtual void OwnedThenUnionDistinct()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            context.ChangeLogs
                .Where(a => a.ChangeLogId > 80)
                .Union(
                    context.ChangeLogs
                        .Where(a => a.ChangeLogId < 20))
                .Union(
                    context.ChangeLogs
                        .Where(a => a.ChangeLogId == 50))
                .TagWith("PruneSelfJoins")
                .Load();
        }

        [ConditionalFact, TestPriority(12)]
        public virtual void Manual_OnlyLeftJoin()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            var query =
                from a in context.Apples.Select(a => new { a.Id })
                join b in context.Bananas.Select(b => new { b.Id }) on a.Id equals b.Id into bb from b in bb.DefaultIfEmpty()
                join t in context.Apples.Where(a0 => a0.Name != null).Select(a0 => new { a0.Id, a0.Name }) on a.Id equals t.Id into aa0 from t in aa0.DefaultIfEmpty()
                join t0 in context.Apples.Where(a1 => a1.Owner != null).Select(a1 => new { a1.Id, a1.Owner }) on t.Id equals t0.Id into aa1 from t0 in aa1.DefaultIfEmpty()
                join t1 in context.Bananas.Where(b0 => b0.Name != null).Select(b0 => new { b0.Id, b0.Name }) on b.Id equals t1.Id into bb0 from t1 in bb0.DefaultIfEmpty()
                join t2 in context.Bananas.Where(b1 => b1.Owner != null).Select(b1 => new { b1.Id, b1.Owner }) on t1.Id equals t2.Id into bb1 from t2 in bb1.DefaultIfEmpty()
                join t3 in context.Bananas.Where(b2 => b2.Source != null).Select(b2 => new { b2.Id, b2.Source }) on t2.Id equals t3.Id into bb2 from t3 in bb2.DefaultIfEmpty()
                select new { a.Id, Id0 = (int?)b.Id, Id1 = (int?)t.Id, t.Name, Id2 = (int?)t0.Id, t0.Owner, Id3 = (int?)t1.Id, Name0 = t1.Name, Id4 = (int?)t2.Id, Owner0 = t2.Owner, Id5 = (int?)t3.Id, t3.Source };

            query.TagWith("PruneSelfJoins").Load();
        }

        [ConditionalFact, TestPriority(13)]
        public virtual void Manual_LeftAndInnerJoin()
        {
            using var context = CreateContext();
            using var scope = CatchCommand();

            var query =
                from a in context.Apples.Select(a => new { a.Id })
                join b in context.Bananas.Select(b => new { b.Id }) on a.Id equals b.Id
                join t in context.Apples.Where(a0 => a0.Name != null).Select(a0 => new { a0.Id, a0.Name }) on a.Id equals t.Id into aa0 from t in aa0.DefaultIfEmpty()
                join t0 in context.Apples.Where(a1 => a1.Owner != null).Select(a1 => new { a1.Id, a1.Owner }) on t.Id equals t0.Id into aa1 from t0 in aa1.DefaultIfEmpty()
                join t1 in context.Bananas.Where(b0 => b0.Name != null).Select(b0 => new { b0.Id, b0.Name }) on b.Id equals t1.Id into bb0 from t1 in bb0.DefaultIfEmpty()
                join t2 in context.Bananas.Where(b1 => b1.Owner != null).Select(b1 => new { b1.Id, b1.Owner }) on t1.Id equals t2.Id into bb1 from t2 in bb1.DefaultIfEmpty()
                join t3 in context.Bananas.Where(b2 => b2.Source != null).Select(b2 => new { b2.Id, b2.Source }) on t2.Id equals t3.Id into bb2 from t3 in bb2.DefaultIfEmpty()
                select new { a.Id, Id0 = b.Id, Id1 = (int?)t.Id, t.Name, Id2 = (int?)t0.Id, t0.Owner, Id3 = (int?)t1.Id, Name0 = t1.Name, Id4 = (int?)t2.Id, Owner0 = t2.Owner, Id5 = (int?)t3.Id, t3.Source };

            query.TagWith("PruneSelfJoins").Load();
        }
    }
}
