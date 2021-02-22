using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Check_UseLessJoinsRemoval
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

    public class UpdateContext : DbContext
    {
        public DbSet<ChangeLog> ChangeLogs { get; set; }

        public DbSet<MiniInfo> MiniInfos { get; set; }

        public DbSet<OwnedThree> OwnedThrees { get; set; }

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
        }

        public void Dispose()
        {
            using var context = ContextFactory();
            context.DropContext();
        }
    }

    [Collection("DatabaseCollection")]
    public class UseLessJoinsRemoval : IClassFixture<NameFixture>
    {
        readonly Func<UpdateContext> contextFactory;

        public UseLessJoinsRemoval(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
        }

        [Fact, TestPriority(0)]
        public void Owned()
        {
            using var context = contextFactory();
            var sql = context.ChangeLogs.ToSQL();
            Assert.DoesNotContain("JOIN", sql);
            context.ChangeLogs.Load();
        }

        [Fact, TestPriority(1)]
        public void HasOneWithOne_SharedTable()
        {
            using var context = contextFactory();
            var query = context.MiniInfos.Include(e => e.Full);

            var sql = query.ToSQL();
            Assert.DoesNotContain("JOIN", sql);
            query.Load();
        }

        [Fact, TestPriority(2)]
        public void SuperOwned()
        {
            using var context = contextFactory();
            var sql = context.OwnedThrees.ToSQL();
            Assert.DoesNotContain("JOIN", sql);
            context.OwnedThrees.Load();
        }

        [Fact, TestPriority(3)]
        public void InnerJoin_3_2()
        {
            using var context = contextFactory();
            var query = context.OwnedThrees
                .Join(context.ChangeLogs, o => o.Id, o => o.ChangeLogId, (o, p) => new { o, p });

            var sql = query.ToSQL();
            Assert.DoesNotContain("LEFT JOIN", sql);
            query.Load();
        }

        [Fact, TestPriority(4)]
        public void InnerJoin_2_3()
        {
            using var context = contextFactory();
            var query = context.ChangeLogs
                .Join(context.OwnedThrees, o => o.ChangeLogId, o => o.Id, (o, p) => new { o, p });

            var sql = query.ToSQL();
            Assert.DoesNotContain("LEFT JOIN", sql);
            query.Load();
        }

        [Fact, TestPriority(5)]
        public void GroupJoin_3_2()
        {
            using var context = contextFactory();
            var query = context.OwnedThrees
                .GroupJoin(context.ChangeLogs, o => o.Id, o => o.ChangeLogId, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p });

            var sql = query.ToSQL();
            Assert.DoesNotContain("INNER JOIN", sql);
            query.Load();
        }

        [Fact, TestPriority(6)]
        public void GroupJoin_2_3()
        {
            using var context = contextFactory();
            var query = context.ChangeLogs
                .GroupJoin(context.OwnedThrees, o => o.ChangeLogId, o => o.Id, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p });

            var sql = query.ToSQL();
            Assert.DoesNotContain("INNER JOIN", sql);
            query.Load();
        }

        [Fact, TestPriority(7)]
        public void ReallyJoin()
        {
            using var context = contextFactory();
            var query = context.OwnedThrees
                .GroupJoin(context.OwnedThrees, o => o.Id, o => o.Happy, (o, p) => new { o, p })
                .SelectMany(a => a.p.DefaultIfEmpty(), (a, p) => new { a.o, p })
                .Join(context.OwnedThrees, o => o.o.Id, o => o.Other, (a, o) => new { a.o, a.p, o2 = o })
                .Where(o => o.o.Other != 3);

            var sql = query.ToSQL();
            Assert.Equal(4, sql.Trim().Count(c => c == '\n'));
            query.Load();
        }
    }
}
