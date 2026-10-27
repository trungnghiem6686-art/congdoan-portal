using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public partial interface IExamDbContext
{
    DbSet<Question> Questions { get; }
    DbSet<QuestionVersion> QuestionVersions { get; }
    DbSet<QuestionImport> QuestionImports { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void ExpectRowVersion(object entity, byte[] rowVersion);

    /// <summary>True when the save failed on a unique index (used to detect lost races).</summary>
    bool IsUniqueViolation(DbUpdateException ex);

    /// <summary>Forget tracked entities after a failed save so the next read sees the database.</summary>
    void ResetTracking();
    Task InTransactionAsync(Func<Task> work, System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);
}

/// <summary>Reads and writes the question import spreadsheet (first worksheet, header row first).</summary>
public interface IQuestionTemplate
{
    IReadOnlyList<string[]> ReadXlsx(Stream xlsx);
    byte[] Xlsx(IEnumerable<string[]> rows);
}
