using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Xunit;

namespace Testcase_Upsert
{
    public class RankCache
    {
        public int ContestId { get; set; }
        public int TeamId { get; set; }
        public int PointsRestricted { get; set; }
        public int TotalTimeRestricted { get; set; }
        public int PointsPublic { get; set; }
        public int TotalTimePublic { get; set; }
    }

    public class RankSource
    {
        public int ContestId { get; set; }
        public int TeamId { get; set; }
        public int Time { get; set; }
        public bool Public { get; set; }
    }

    public class TwoRelation
    {
        public int AaaId { get; set; }
        public int BbbId { get; set; }
    }

    public class ThreeRelation
    {
        public int AaaId { get; set; }
        public int BbbId { get; set; }
        public int PkeyId { get; set; }
    }

    public class UpsertContext : DbContext
    {
        public DbSet<RankCache> RankCache { get; set; }

        public DbSet<RankSource> RankSource { get; set; }

        public DbSet<TwoRelation> TwoRelations { get; set; }

        public DbSet<ThreeRelation> ThreeRelations { get; set; }

        public string DefaultSchema { get; }

        public UpsertContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RankCache>(entity =>
            {
                entity.ToTable(nameof(RankCache) + "_" + DefaultSchema);

                entity.HasKey(e => new { e.ContestId, e.TeamId });
            });

            modelBuilder.Entity<RankSource>(entity =>
            {
                entity.ToTable(nameof(RankSource) + "_" + DefaultSchema);

                entity.HasKey(e => new { e.ContestId, e.TeamId });
            });

            modelBuilder.Entity<TwoRelation>(entity =>
            {
                entity.ToTable(nameof(TwoRelation) + "_" + DefaultSchema);

                entity.HasKey(e => new { e.BbbId, e.AaaId });

                entity.HasIndex(e => e.AaaId);
            });

            modelBuilder.Entity<ThreeRelation>(entity =>
            {
                entity.ToTable(nameof(ThreeRelation) + "_" + DefaultSchema);

                entity.HasKey(e => e.PkeyId);

                entity.HasAlternateKey(e => new { e.BbbId, e.AaaId });

                entity.HasIndex(e => e.AaaId);
            });
        }
    }

    public sealed class NameFixture : IDisposable
    {
        public string Schema { get; }

        public Func<UpsertContext> ContextFactory { get; }

        public NameFixture()
        {
            ContextFactory = ContextUtil.MakeContextFactory<UpsertContext>();
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
    public sealed class UpsertSql : IClassFixture<NameFixture>
    {
        readonly Func<UpsertContext> contextFactory;

        public UpsertSql(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
        }

        private void EnsureRank(RankSource[] sources = null, RankCache[] caches = null)
        {
            caches ??= new[]
            {
                new RankCache { TeamId = 2, ContestId = 1, PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = 9, TotalTimeRestricted = 9 },
                new RankCache { TeamId = 3, ContestId = 1, PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = 9, TotalTimeRestricted = 9 }
            };

            sources ??= new[]
            {
                new RankSource { ContestId = 1, TeamId = 2, Public = true, Time = 50 },
                new RankSource { ContestId = 3, TeamId = 4, Public = false, Time = 50 }
            };

            using var context = contextFactory();
            context.RankCache.BatchDelete();
            context.RankSource.BatchDelete();

            context.RankSource.AddRange(sources);
            context.RankCache.AddRange(caches);
            context.SaveChanges();
        }

        [Fact, TestPriority(0)]
        public void Upsert_NewAnonymousObject()
        {
            EnsureRank(Array.Empty<RankSource>());
            using var context = contextFactory();

            var e = context.RankCache.Upsert(
                new[]
                {
                    new { ContestId = 1, TeamId = 2, Time = 50 },
                    new { ContestId = 3, TeamId = 4, Time = 50 },
                },
                rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });

            Assert.Equal(3, context.RankCache.Count());
        }

        [Fact, TestPriority(1)]
        public void Upsert_AnotherTable()
        {
            EnsureRank();

            using var context = contextFactory();
            
            var e = context.RankCache.Upsert(
                context.RankSource,
                rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });

            Assert.Equal(3, context.RankCache.Count());
        }

#if !IN_MEMORY
        [Fact, TestPriority(2)]
#endif
        public void Upsert_FromSql()
        {
            EnsureRank();

            using var context = contextFactory();
            var query = context.RankSource.ToParametrizedSql().Item1;

            var e = context.RankCache.Upsert(
                context.RankSource.FromSqlRaw(query.Trim()),
                rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });

            Assert.Equal(3, context.RankCache.Count());
        }

        [Fact, TestPriority(3)]
        public void Upsert_SubSelect()
        {
            EnsureRank();

            using var context = contextFactory();

            var e = context.RankCache.Upsert(
                context.RankSource.Distinct(),
                rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });

            Assert.Equal(3, context.RankCache.Count());
        }

        [Fact, TestPriority(4)]
        public void InsertIfNotExists_AnotherTable()
        {
            EnsureRank();

            using var context = contextFactory();

            var e = context.RankCache.Upsert(
                context.RankSource,
                rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, });

            Assert.Equal(3, context.RankCache.Count());
        }

        [Fact(Skip = "Future"), TestPriority(5)]
        public void Upsert_Bug202102102024()
        {
            using var context = contextFactory();

            Assert.Equal(0, context.TwoRelations.Count());

            int aaa = 1, bbb = 1;
            var e = context.TwoRelations.Upsert(
                new { aaa, bbb },
                s => new TwoRelation { BbbId = bbb, AaaId = aaa });

            Assert.Equal(1, context.TwoRelations.Count());
        }

        [Fact, TestPriority(6)]
        public void Upsert_AlternativeKey()
        {
            using var context = contextFactory();

            Assert.Equal(0, context.ThreeRelations.Count());

            int aaa = 1, bbb = 1;
            var e = context.ThreeRelations.Upsert(
                new { aaa, bbb },
                s => new ThreeRelation { BbbId = s.bbb, AaaId = s.aaa });

            Assert.Equal(1, context.ThreeRelations.Count());
        }
    }
}
