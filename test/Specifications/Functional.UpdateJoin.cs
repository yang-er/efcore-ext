using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.BatchUpdateJoin
{
    public class ItemA
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    public class ItemB
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    public class UpdateContext : DbContext
    {
        public DbSet<ItemA> A { get; set; }
        public DbSet<ItemB> B { get; set; }
        public string DefaultSchema { get; }

        public UpdateContext(string schema, DbContextOptions options)
            : base(options)
        {
            DefaultSchema = schema;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ItemA>(entity =>
            {
                entity.Property(a => a.Id).ValueGeneratedNever();
                entity.ToTable(nameof(ItemA) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<ItemB>(entity =>
            {
                entity.Property(a => a.Id).ValueGeneratedNever();
                entity.ToTable(nameof(ItemB) + "_" + DefaultSchema);
            });
        }
    }

    public sealed class DataFixture<TFactory> :
        IClassFixture<IDbContextFactory<UpdateContext>>
        where TFactory : class, IDbContextFactory<UpdateContext>
    {
        public DataFixture(TFactory factory)
        {
            using var context = factory.Create();
            context.A.Add(new ItemA { Id = 1, Value = 1 });
            context.B.Add(new ItemB { Id = 1, Value = 2 });
            context.A.Add(new ItemA { Id = 2, Value = 1 });
            context.B.Add(new ItemB { Id = 2, Value = 2 });
            context.SaveChanges();
        }
    }

    public abstract class UpdateJoinTestBase<TFactory> :
        QueryTestBase<UpdateContext, TFactory>,
        IClassFixture<DataFixture<TFactory>>
        where TFactory : class, IDbContextFactory<UpdateContext>
    {
        protected UpdateJoinTestBase(
            TFactory factory,
            DataFixture<TFactory> dataFixture)
            : base(factory)
        {
            using var context = factory.Create();
            context.A.Add(new ItemA { Id = 1, Value = 1 });
            context.B.Add(new ItemB { Id = 1, Value = 2 });
            context.A.Add(new ItemA { Id = 2, Value = 1 });
            context.B.Add(new ItemB { Id = 2, Value = 2 });
            context.SaveChanges();
        }

        [ConditionalFact, TestPriority(-1)]
        public void NormalUpdate()
        {
            using (CatchCommand())
            {
                using var context = CreateContext();

                context.A.BatchUpdateJoin(
                    inner: context.B.Where(b => b.Value == 2),
                    outerKeySelector: a => a.Id,
                    innerKeySelector: b => b.Id,
                    condition: (a, b) => a.Id == 1,
                    updateSelector: (a, b) => new ItemA { Value = a.Value + b.Value - 3 });
            }

            using (var context = CreateContext())
            {
                var list = context.A.OrderBy(a => a.Id).ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(1, list[0].Id);
                Assert.Equal(0, list[0].Value);
                Assert.Equal(2, list[1].Id);
                Assert.Equal(1, list[1].Value);
            }
        }

        [ConditionalFact, TestPriority(0)]
        public void LocalTableJoin()
        {
            var lst = new[]
            {
                new { Id = 1, Value = 3 },
                new { Id = 2, Value = 4 },
                new { Id = 3, Value = 5 },
            };

            using (CatchCommand())
            {
                using var context = CreateContext();

                context.B.BatchUpdateJoin(
                    inner: lst,
                    outerKeySelector: a => a.Id,
                    innerKeySelector: b => b.Id,
                    condition: (a, b) => a.Id != 2,
                    updateSelector: (a, b) => new ItemB { Value = a.Value + b.Value });
            }

            using (var context = CreateContext())
            {
                var list = context.B.OrderBy(a => a.Id).ToList();
                Assert.Equal(2, list.Count);
                Assert.Equal(1, list[0].Id);
                Assert.Equal(5, list[0].Value);
                Assert.Equal(2, list[1].Id);
                Assert.Equal(2, list[1].Value);
            }
        }

        [ConditionalFact, TestPriority(1)]
        public void CompiledQuery_NormalUpdate()
        {
            var compiledQuery = EF.CompileQuery(
                (UpdateContext ctx, int aa, int bb, int cc)
                    => ctx.A.BatchUpdateJoin(
                        ctx.B.Where(b => b.Value == aa),
                        a => a.Id,
                        b => b.Id,
                        (a, b) => new ItemA { Value = a.Value + b.Value - cc },
                        (a, b) => a.Id == bb));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery.Invoke(context, 5, 6, 7);
                compiledQuery.Invoke(context, 2, 5, 7);
            }
        }
    }
}
