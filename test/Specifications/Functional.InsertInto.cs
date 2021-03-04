using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.BatchInsertInto
{
    public abstract class Person
    {
        public int PersonId { get; set; }
        public string Name { get; set; }
    }

    public class Student : Person
    {
        public string Subject { get; set; }
    }

    public class Teacher : Person
    {
        public string Class { get; set; }
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

    public class Document
    {
        public int DocumentId { get; set; }
        [Required] public string Content { get; set; }
        [Timestamp] public byte[] VersionChange { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] public int ContentLength { get; set; }
    }

    public class SelectIntoContext : DbContext
    {
        public DbSet<Person> Persons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<Judging> Judgings { get; set; }
        public DbSet<Document> Documents { get; set; }
        public string DefaultSchema { get; }

        public SelectIntoContext(string schema, DbContextOptions options)
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

            modelBuilder.Entity<Person>(entity =>
            {
                entity.ToTable(nameof(Person) + "_" + DefaultSchema);
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable(nameof(Document) + "_" + DefaultSchema);
                entity.Property(p => p.ContentLength)
                    .HasComputedColumnSql(
                        Database.IsSqlServer() ? "CONVERT([int], len([Content]))" :
                        Database.IsNpgsql() ? "length(\"Content\")" :
                        Database.IsInMemory() ? "" :
                        throw new NotImplementedException(),
                        true);
            });
        }
    }

    public abstract class InsertIntoTestBase<TFactory> :
        QueryTestBase<SelectIntoContext, TFactory>
        where TFactory : class, IDbContextFactory<SelectIntoContext>
    {
        protected InsertIntoTestBase(TFactory factory) : base(factory)
        {
        }

        [ConditionalFact, TestPriority(0)]
        public void NormalSelectInto()
        {
            string hh = "HHH";
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Judgings
                .Select(ih => new ChangeLog
                {
                    Description = (ih.Server ?? hh) + "666",
                    Audit = new Audit
                    {
                        ChangedBy = ih.CompileError,
                        IsDeleted = true
                    }
                })
                .BatchInsertInto(context.ChangeLogs);
        }

        [ConditionalFact, TestPriority(1)]
        public void WithAbstractType()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Students
                .Select(s => new Teacher
                {
                    Name = s.Name,
                    Class = s.Subject,
                })
                .BatchInsertInto(context.Teachers);
        }

        [ConditionalFact, TestPriority(2)]
        public void WithComputedColumn()
        {
            using var scope = CatchCommand();
            using var context = CreateContext();

            context.Documents
                .Select(s => new Document
                {
                    Content = s.Content + s.ContentLength.ToString(),
                })
                .BatchInsertInto(context.Documents);
        }

        [ConditionalFact, TestPriority(3)]
        public void CompiledQuery_NormalSelectInto()
        {
            var compiledQuery = EF.CompileQuery(
                (SelectIntoContext ctx, string hh)
                    => ctx.Judgings
                        .Select(ih => new ChangeLog
                        {
                            Description = (ih.Server ?? hh) + "666",
                            Audit = new Audit
                            {
                                ChangedBy = ih.CompileError,
                                IsDeleted = true
                            }
                        })
                        .BatchInsertInto(ctx.ChangeLogs));

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, "aa");
            }

            using (CatchCommand())
            {
                using var context = CreateContext();
                compiledQuery(context, "bb");
            }
        }

        [ConditionalFact, TestPriority(4)]
        public void CompiledQuery_WithAbstractType()
        {
            var compiledQuery = EF.CompileQuery(
                (SelectIntoContext ctx)
                    => ctx.Students
                        .Select(s => new Teacher
                        {
                            Name = s.Name,
                            Class = s.Subject,
                        })
                        .BatchInsertInto(ctx.Teachers));

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

        [ConditionalFact, TestPriority(5)]
        public void CompiledQuery_WithComputedColumn()
        {
            var compiledQuery = EF.CompileQuery(
                (SelectIntoContext ctx)
                    => ctx.Documents
                        .Select(s => new Document
                        {
                            Content = s.Content + s.ContentLength.ToString(),
                        })
                        .BatchInsertInto(ctx.Documents));

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
