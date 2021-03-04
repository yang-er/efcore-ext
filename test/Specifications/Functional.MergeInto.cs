﻿using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
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

    public class MergeContext : DbContext
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
    }

    public class DataFixture<TFactory> : IClassFixture<TFactory>
        where TFactory : class, IDbContextFactory<MergeContext>
    {
        public DataFixture(TFactory factory)
        {
            using var context = factory.Create();

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
    }

    [DatabaseProviderSkipCondition(DatabaseProvider.PostgreSQL)]
    public abstract class MergeIntoTestBase<TFactory> :
        QueryTestBase<MergeContext, TFactory>,
        IClassFixture<DataFixture<TFactory>>
        where TFactory : class, IDbContextFactory<MergeContext>
    {
        protected MergeIntoTestBase(TFactory factory) : base(factory)
        {
        }

        [ConditionalFact, TestPriority(0)]
        public void Upsert()
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
        public void Synchronize()
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
        public void SourceFromSql()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();

            var ans = context.RankCache.Merge(
                sourceTable: GetSqlRawForRankSource(),
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

        protected abstract IQueryable<RankSource> GetSqlRawForRankSource();
    }
}
