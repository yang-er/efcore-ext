using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace Testcase_BatchInsertInto
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
                    .HasComputedColumnSql(ContextUtil.ConvertLenContentToInt, true);
            });
        }
    }

    public sealed class NameFixture : IDisposable
    {
        public string Schema { get; }

        public Func<SelectIntoContext> ContextFactory { get; }

        public NameFixture()
        {
            Schema = Guid.NewGuid().ToString()[0..6];
            ContextFactory = ContextUtil.MakeContextFactory<SelectIntoContext>();
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
    public class InsertIntoSql : IClassFixture<NameFixture>
    {
        readonly Func<SelectIntoContext> contextFactory;

        public InsertIntoSql(NameFixture nameFixture)
        {
            contextFactory = nameFixture.ContextFactory;
        }

        [ConditionalFact, TestPriority(0)]
        public void NormalSelectInto()
        {
            using var context = contextFactory();
            string hh = "HHH";

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
            using var context = contextFactory();

            context.Students
                .Select(s => new Teacher
                {
                    Name = s.Name,
                    Class = s.Subject,
                })
                .BatchInsertInto(context.Teachers);
        }

        [ConditionalFact, TestPriority(1)]
        public void WithComputedColumn()
        {
            using var context = contextFactory();

            context.Documents
                .Select(s => new Document
                {
                    Content = s.Content + s.ContentLength.ToString(),
                })
                .BatchInsertInto(context.Documents);
        }
    }
}
