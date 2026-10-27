using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Infrastructure.Exams;

public sealed partial class ExamDbContext
{
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamMatrixRule> ExamMatrixRules => Set<ExamMatrixRule>();
    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();
    public DbSet<ExamTemplate> ExamTemplates => Set<ExamTemplate>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<GradeRevision> GradeRevisions => Set<GradeRevision>();
    public DbSet<AttemptEvent> AttemptEvents => Set<AttemptEvent>();

    partial void ConfigureExams(ModelBuilder b)
    {
        b.Entity<Exam>(e =>
        {
            e.ToTable("Exams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Instructions).HasMaxLength(4000);
            e.Property(x => x.PassScore).HasPrecision(5, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.Status, x.IsDeleted, x.OpensAtUtc });
        });
        b.Entity<ExamQuestion>(e =>
        {
            e.ToTable("ExamQuestions");
            e.HasKey(x => new { x.ExamId, x.QuestionId });
            e.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ExamMatrixRule>(e =>
        {
            e.ToTable("ExamMatrixRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Topic).HasMaxLength(200);
            e.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ExamAssignment>(e =>
        {
            e.ToTable("ExamAssignments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ExamId, x.TargetType, x.TargetId }).IsUnique();
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ExamTemplate>(e =>
        {
            e.ToTable("ExamTemplates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
        });
        b.Entity<Attempt>(e =>
        {
            e.ToTable("Attempts");
            e.HasKey(x => x.Id);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.Points).HasPrecision(9, 2);
            e.Property(x => x.MaxScore).HasPrecision(9, 2);
            e.Property(x => x.Score).HasPrecision(6, 3);
            e.Property(x => x.WriterToken).HasMaxLength(64);
            // The two invariants of the exam core, enforced by the database itself.
            e.HasIndex(x => new { x.ExamId, x.CandidateId }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UX_Attempt_OneInProgress");
            e.HasIndex(x => new { x.ExamId, x.CandidateId, x.AttemptNo }).IsUnique().HasDatabaseName("UX_Attempt_No");
            e.HasIndex(x => new { x.Status, x.DeadlineUtc });
            e.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AttemptQuestion>(e =>
        {
            e.ToTable("AttemptQuestions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Points).HasPrecision(9, 2);
            e.HasIndex(x => new { x.AttemptId, x.OrderNo }).IsUnique();
            e.HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Answer>(e =>
        {
            e.ToTable("Answers");
            e.HasKey(x => new { x.AttemptId, x.AttemptQuestionId });
            e.Property(x => x.AwardedPoints).HasPrecision(9, 2);
            e.HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AttemptQuestion>().WithMany().HasForeignKey(x => x.AttemptQuestionId).OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<GradeRevision>(e =>
        {
            e.ToTable("GradeRevisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.OldScore).HasPrecision(6, 3);
            e.Property(x => x.NewScore).HasPrecision(6, 3);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AttemptEvent>(e =>
        {
            e.ToTable("AttemptEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(500);
            e.HasIndex(x => x.AttemptId);
        });
    }
}
