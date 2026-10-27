using System.Data;
using CongDoan.Application.Exams;
using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CongDoan.Infrastructure.Exams;

public sealed partial class ExamDbContext : DbContext, IExamDbContext
{
    public const string Schema = "exam";

    public ExamDbContext(DbContextOptions<ExamDbContext> options) : base(options) { }

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<QuestionImport> QuestionImports => Set<QuestionImport>();

    public void ExpectRowVersion(object entity, byte[] rowVersion) => Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

    public bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    public void ResetTracking() => ChangeTracker.Clear();

    public async Task InTransactionAsync(Func<Task> work, IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            ChangeTracker.Clear();
            await using var tx = await Database.BeginTransactionAsync(isolation, ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);
        b.Entity<Question>(e =>
        {
            e.ToTable("Questions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Topic).HasMaxLength(200).IsRequired();
            e.Property(x => x.SourceKey).HasMaxLength(300);
            e.HasIndex(x => x.SourceKey).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Topic, x.Difficulty, x.Status, x.IsDeleted });
        });
        b.Entity<QuestionVersion>(e =>
        {
            e.ToTable("QuestionVersions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.QuestionId, x.VersionNo }).IsUnique();
            e.Property(x => x.Points).HasPrecision(9, 2);
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Choices).WithOne().HasForeignKey(x => x.QuestionVersionId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Choices).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<Choice>(e =>
        {
            e.ToTable("Choices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        });
        b.Entity<QuestionImport>(e =>
        {
            e.ToTable("QuestionImports");
            e.HasKey(x => x.Id);
        });
        ConfigureExams(b);
    }

    partial void ConfigureExams(ModelBuilder b);
}

internal sealed class ExamDesignFactory : IDesignTimeDbContextFactory<ExamDbContext>
{
    public ExamDbContext CreateDbContext(string[] args) => new(DesignTime.Options<ExamDbContext>(ExamDbContext.Schema));
}
