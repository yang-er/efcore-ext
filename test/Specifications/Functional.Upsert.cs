using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.Upsert
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

    public abstract class UpsertTestBase<TFactory> : QueryTestBase<UpsertContext, TFactory>
        where TFactory : class, IDbContextFactory<UpsertContext>
    {
        protected UpsertTestBase(TFactory factory) : base(factory)
        {
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

            using (var context = CreateContext())
            {
                var rc = context.RankCache.ToList();
                var rs = context.RankSource.ToList();
                context.RankSource.RemoveRange(rs);
                context.RankCache.RemoveRange(rc);
                context.SaveChanges();
            }

            using (var context = CreateContext())
            {
                context.RankSource.AddRange(sources);
                context.RankCache.AddRange(caches);
                context.SaveChanges();
            }
        }

        [ConditionalFact, TestPriority(0)]
        public virtual void Upsert_NewAnonymousObject()
        {
            EnsureRank(Array.Empty<RankSource>());

            var data = new[]
            {
                new { ContestId = 1, TeamId = 2, Time = 50 },
                new { ContestId = 3, TeamId = 4, Time = 50 },
            };

            using (CatchCommand())
            {
                using var context = CreateContext();
                var e = context.RankCache.Upsert(
                    data,
                    rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                    (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(1)]
        public virtual void Upsert_AnotherTable()
        {
            EnsureRank();

            using (CatchCommand())
            {
                using var context = CreateContext();

                context.RankCache.Upsert(
                    context.RankSource,
                    rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                    (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(2)]
        [DatabaseProviderSkipCondition(DatabaseProvider.InMemory)]
        public virtual void Upsert_FromSql()
        {
            EnsureRank();

            using (CatchCommand())
            {
                using var context = CreateContext();
                var sql = context.RankSource.ToSQL();

                context.RankCache.Upsert(
                    context.RankSource.FromSqlRaw(sql),
                    rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                    (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(3)]
        public virtual void Upsert_SubSelect()
        {
            EnsureRank();

            using (CatchCommand())
            {
                using var context = CreateContext();

                context.RankCache.Upsert(
                    context.RankSource.Distinct(),
                    rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, },
                    (rc, rc2) => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + rc2.TotalTimePublic, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(4)]
        public virtual void InsertIfNotExists_AnotherTable()
        {
            EnsureRank();

            using (CatchCommand())
            {
                using var context = CreateContext();

                var e = context.RankCache.Upsert(
                    context.RankSource,
                    rc2 => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = rc2.Time, TotalTimeRestricted = rc2.Time, ContestId = rc2.ContestId, TeamId = rc2.TeamId, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(5)]
        public virtual void Translation_Parameterize()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(0, context.TwoRelations.Count());
            }

            int aaa = 1, bbb = 1;
            using (CatchCommand())
            {
                using var context = CreateContext();
                context.TwoRelations.Upsert(
                    new { aaa, bbb },
                    s => new TwoRelation { BbbId = bbb, AaaId = aaa });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(1, context.TwoRelations.Count());
            }
        }

        [ConditionalFact, TestPriority(6)]
        public virtual void Upsert_AlternativeKey()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(0, context.ThreeRelations.Count());
            }

            int aaa = 1, bbb = 1;
            using (CatchCommand())
            {
                using var context = CreateContext();
                context.ThreeRelations.Upsert(
                    new { aaa, bbb },
                    s => new ThreeRelation { BbbId = s.bbb, AaaId = s.aaa });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(1, context.ThreeRelations.Count());
            }
        }

        [ConditionalFact, TestPriority(7)]
        public virtual void UpsertOne()
        {
            EnsureRank(Array.Empty<RankSource>());

            using (CatchCommand())
            {
                int cid = 1, teamid = 2, time = 50;

                using var context = CreateContext();
                var e = context.RankCache.Upsert(
                    () => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = time, TotalTimeRestricted = time, ContestId = cid, TeamId = teamid, },
                    rc => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + time, });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(2, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(7)]
        public virtual void UpsertOne_CompiledQuery()
        {
            EnsureRank(Array.Empty<RankSource>());

            var compiledQuery = EF.CompileQuery(
                (UpsertContext ctx, int cid, int teamid, int time)
                    => ctx.RankCache.Upsert(
                        () => new RankCache { ContestId = cid, TeamId = teamid, PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = time, TotalTimeRestricted = time },
                        rc => new RankCache { PointsPublic = rc.PointsPublic + 1, TotalTimePublic = rc.TotalTimePublic + time }));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 1, 2, 50);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 3, 4, 50);
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(7)]
        public virtual void InsertIfNotExistOne()
        {
            EnsureRank(Array.Empty<RankSource>());

            using (CatchCommand())
            {
                int cid = 1, teamid = 2, time = 50;

                using var context = CreateContext();
                var e = context.RankCache.Upsert(
                    () => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = time, TotalTimeRestricted = time, ContestId = cid, TeamId = teamid });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(2, context.RankCache.Count());
            }

            using (CatchCommand())
            {
                int cid = 3, teamid = 4, time = 50;

                using var context = CreateContext();
                var e = context.RankCache.Upsert(
                    () => new RankCache { PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = time, TotalTimeRestricted = time, ContestId = cid, TeamId = teamid });
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(7)]
        public virtual void InsertIfNotExistOne_CompiledQuery()
        {
            EnsureRank(Array.Empty<RankSource>());

            var compiledQuery = EF.CompileQuery(
                (UpsertContext ctx, int cid, int teamid, int time)
                    => ctx.RankCache.Upsert(
                        () => new RankCache { ContestId = cid, TeamId = teamid, PointsPublic = 1, PointsRestricted = 1, TotalTimePublic = time, TotalTimeRestricted = time },
                        null));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 1, 2, 50);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 3, 4, 50);
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }
    }
}
