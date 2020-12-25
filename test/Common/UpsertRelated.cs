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

    public class UpsertContext : DbContext
    {
        public DbSet<RankCache> RankCache { get; set; }

        public DbSet<RankSource> RankSource { get; set; }

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

        [Fact, TestPriority(0)]
        public void Upsert_NewAnonymousObject()
        {
            using (var context = contextFactory())
            {
                context.RankCache.BatchDelete();
                context.RankSource.BatchDelete();

                context.RankSource.AddRange(
                    new RankSource { ContestId = 2, TeamId = 1, Public = true, Time = 100 },
                    new RankSource { ContestId = 1, TeamId = 2, Public = false, Time = 77 });

                context.RankCache.AddRange(
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

                context.SaveChanges();
            }

            using (var context = contextFactory())
            {
                var ot = new[]
                {
                    new { ContestId = 1, TeamId = 2, Time = 50 },
                    new { ContestId = 3, TeamId = 4, Time = 50 },
                };

                var e = context.RankCache.Upsert(
                    ot,
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
                    updateExpression:
                        (rc, rc2) => new RankCache
                        {
                            PointsPublic = rc.PointsPublic + 1,
                            TotalTimePublic = rc.TotalTimePublic + rc2.Time,
                        });
            }

            using (var context = contextFactory())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }

        [Fact, TestPriority(0)]
        public void Upsert_AnotherTable()
        {
            using (var context = contextFactory())
            {
                context.RankCache.BatchDelete();
                context.RankSource.BatchDelete();

                context.RankSource.AddRange(
                    new RankSource { ContestId = 1, TeamId = 2, Public = true, Time = 50 },
                    new RankSource { ContestId = 3, TeamId = 4, Public = false, Time = 50 });

                context.RankCache.AddRange(
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

                context.SaveChanges();
            }

            using (var context = contextFactory())
            {
                var e = context.RankCache.Upsert(
                    context.RankSource,
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
                    updateExpression:
                        (rc, rc2) => new RankCache
                        {
                            PointsPublic = rc.PointsPublic + 1,
                            TotalTimePublic = rc.TotalTimePublic + rc2.Time,
                        });
            }

            using (var context = contextFactory())
            {
                Assert.Equal(3, context.RankCache.Count());
            }
        }
    }
}
