using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.MergeInto
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

    public class MergeContext : DbContext, IDbContextWithSeeds
    {
        public DbSet<RankCache> RankCache { get; set; }
        public DbSet<RankSource> RankSource { get; set; }
        public string DefaultSchema { get; }

        public MergeContext(string schema, DbContextOptions options)
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
        }

        public object Seed()
        {
            RankSource.AddRange(
                new RankSource { ContestId = 2, TeamId = 1, Public = true, Time = 100 },
                new RankSource { ContestId = 1, TeamId = 2, Public = false, Time = 77 });

            RankCache.AddRange(
                new RankCache
                {
                    TeamId = 2,
                    ContestId = 1,
                    PointsPublic = 1,
                    PointsRestricted = 1,
                    TotalTimePublic = 9,
                    TotalTimeRestricted = 9
                },
                new RankCache
                {
                    TeamId = 3,
                    ContestId = 1,
                    PointsPublic = 1,
                    PointsRestricted = 1,
                    TotalTimePublic = 9,
                    TotalTimeRestricted = 9
                });

            SaveChanges();
            return null;
        }
    }

    [DatabaseProviderSkipCondition(DatabaseProvider.PostgreSQL)]
    public abstract class MergeIntoTestBase<TFactory> : QueryTestBase<MergeContext, TFactory>
        where TFactory : class, IDbContextFactory<MergeContext>
    {
        protected MergeIntoTestBase(TFactory factory) : base(factory)
        {
        }

        [ConditionalFact, TestPriority(0)]
        public virtual void Upsert()
        {
            using (CatchCommand())
            {
                using var context = CreateContext();

                var ot = new[]
                {
                    new { ContestId = 1, TeamId = 2, Time = 50 },
                    new { ContestId = 3, TeamId = 4, Time = 50 },
                };

                context.RankCache.Merge(
                    sourceTable: ot,
                    targetKey: rc => new { rc.ContestId, rc.TeamId },
                    sourceKey: rc => new { rc.ContestId, rc.TeamId },
                    updateExpression:
                        (rc, rc2) => new RankCache
                        {
                            PointsPublic = rc.PointsPublic + 1,
                            TotalTimePublic = rc.TotalTimePublic + rc2.Time,
                        },
                    insertExpression:
                        rc2 => new RankCache
                        {
                            PointsPublic = 1,
                            PointsRestricted = 1,
                            TotalTimePublic = rc2.Time,
                            TotalTimeRestricted = rc2.Time,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                        },
                    delete: false);
            }

            using (var context = CreateContext())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [ConditionalFact, TestPriority(1)]
        public virtual void Synchronize()
        {
            using (CatchCommand())
            {
                using var context = CreateContext();

                context.RankCache.Merge(
                    sourceTable: context.RankSource,
                    targetKey: rc => new { rc.ContestId, rc.TeamId },
                    sourceKey: rc => new { rc.ContestId, rc.TeamId },
                    updateExpression:
                        (rc, rc2) => new RankCache
                        {
                            PointsPublic = rc2.Public ? rc.PointsPublic + 1 : rc.PointsPublic,
                            TotalTimePublic = rc2.Public ? rc.TotalTimePublic + rc2.Time : rc.TotalTimePublic,
                            PointsRestricted = rc.PointsRestricted + 1,
                            TotalTimeRestricted = rc.TotalTimeRestricted + rc2.Time,
                        },
                    insertExpression:
                        rc2 => new RankCache
                        {
                            PointsPublic = rc2.Public ? 1 : 0,
                            PointsRestricted = 1,
                            TotalTimePublic = rc2.Public ? rc2.Time : 0,
                            TotalTimeRestricted = rc2.Time,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                        },
                    delete: true);
            }

            using (var context = CreateContext())
            {
                var contents = context.RankCache
                    .OrderBy(rc => rc.ContestId)
                    .ThenBy(rc => rc.TeamId)
                    .ToList();
                Assert.Equal(2, contents.Count);
                Assert.Equal(86, contents[0].TotalTimeRestricted);
                Assert.Equal(100, contents[1].TotalTimeRestricted);
            }
        }

        [ConditionalFact, TestPriority(2)]
        [DatabaseProviderSkipCondition(DatabaseProvider.InMemory)]
        public virtual void SourceFromSql()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();
            var sql = context.RankSource.ToSQL();

            context.RankCache.Merge(
                sourceTable: context.RankSource.FromSqlRaw(sql),
                targetKey: rc => new { rc.ContestId, rc.TeamId },
                sourceKey: rc => new { rc.ContestId, rc.TeamId },
                updateExpression:
                    (rc, rc2) => new RankCache
                    {
                        PointsPublic = rc2.Public ? rc.PointsPublic + 1 : rc.PointsPublic,
                        TotalTimePublic = rc2.Public ? rc.TotalTimePublic + rc2.Time : rc.TotalTimePublic,
                        PointsRestricted = rc.PointsRestricted + 1,
                        TotalTimeRestricted = rc.TotalTimeRestricted + rc2.Time,
                    },
                insertExpression:
                    rc2 => new RankCache
                    {
                        PointsPublic = rc2.Public ? 1 : 0,
                        PointsRestricted = 1,
                        TotalTimePublic = rc2.Public ? rc2.Time : 0,
                        TotalTimeRestricted = rc2.Time,
                        ContestId = rc2.ContestId,
                        TeamId = rc2.TeamId,
                    },
                delete: true);
        }

        [ConditionalFact, TestPriority(3)]
        public virtual void Synchronize_LocalTable()
        {
            using (CatchCommand())
            {
                using var context = CreateContext();

                var table = new[]
                {
                    new { ContestId = 1, TeamId = 2 },
                    new { ContestId = 1, TeamId = 1 },
                };

                context.RankSource.Merge(
                    sourceTable: table,
                    targetKey: rc => new { rc.ContestId, rc.TeamId },
                    sourceKey: rc => new { rc.ContestId, rc.TeamId },
                    updateExpression:
                        (rc, rc2) => new RankSource
                        {
                            Time = 536,
                        },
                    insertExpression:
                        rc2 => new RankSource
                        {
                            Time = 366,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                            Public = true,
                        },
                    delete: true);
            }

            using (var context = CreateContext())
            {
                var contents = context.RankSource
                    .OrderBy(rc => rc.ContestId)
                    .ThenBy(rc => rc.TeamId)
                    .ToList();
                Assert.Equal(2, contents.Count);
                Assert.Equal(366, contents[0].Time);
                Assert.Equal(536, contents[1].Time);
            }
        }

        [ConditionalFact, TestPriority(4)]
        public virtual void Synchronize_LocalTable_Compiled()
        {
            var compiledQuery = EF.CompileQuery(
                (MergeContext ctx, int teamid1, int cid2, int teamid2)
                    => ctx.RankSource.Merge(
                        new[]
                        {
                            new { ContestId = 1, TeamId = teamid1 },
                            new { ContestId = cid2, TeamId = teamid2 },
                        },
                        rc => new { rc.ContestId, rc.TeamId },
                        rc => new { rc.ContestId, rc.TeamId },
                        (rc, rc2) => new RankSource
                        {
                            Time = 536,
                        },
                        rc2 => new RankSource
                        {
                            Time = 366,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                            Public = true,
                        },
                        true));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 2, 1, 1);
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, 2, 1, 1);
            }
        }

        [ConditionalFact, TestPriority(5)]
        public virtual void Synchronize_RemoteTable_Compiled()
        {
            var compiledQuery = EF.CompileQuery(
                (MergeContext ctx) =>
                    ctx.RankCache.Merge(
                        ctx.RankSource,
                        rc => new { rc.ContestId, rc.TeamId },
                        rc => new { rc.ContestId, rc.TeamId },
                        (rc, rc2) => new RankCache
                        {
                            PointsPublic = rc2.Public ? rc.PointsPublic + 1 : rc.PointsPublic,
                            TotalTimePublic = rc2.Public ? rc.TotalTimePublic + rc2.Time : rc.TotalTimePublic,
                            PointsRestricted = rc.PointsRestricted + 1,
                            TotalTimeRestricted = rc.TotalTimeRestricted + rc2.Time,
                        },
                        rc2 => new RankCache
                        {
                            PointsPublic = rc2.Public ? 1 : 0,
                            PointsRestricted = 1,
                            TotalTimePublic = rc2.Public ? rc2.Time : 0,
                            TotalTimeRestricted = rc2.Time,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                        },
                        true));

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

        [ConditionalFact, TestPriority(6)]
        public virtual void Synchronize_NonAssignable_Compiled_ShouldFail()
        {
            var compiledQuery = EF.CompileQuery(
                (MergeContext ctx, int cid1, int teamid1, int cid2, int teamid2)
                    => ctx.RankSource.Merge(
                        new[]
                        {
                            new { ContestId = cid1, TeamId = teamid1 },
                            new { ContestId = cid2, TeamId = teamid2 },
                        }
                        .Where(a => false),
                        rc => new { rc.ContestId, rc.TeamId },
                        rc => new { rc.ContestId, rc.TeamId },
                        (rc, rc2) => new RankSource
                        {
                            Time = 536,
                        },
                        rc2 => new RankSource
                        {
                            Time = 366,
                            ContestId = rc2.ContestId,
                            TeamId = rc2.TeamId,
                            Public = true,
                        },
                        true));

            using var context = CreateContext();
            Assert.Throws<InvalidOperationException>(() => compiledQuery(context, 1, 2, 1, 1));
        }
    }
}
