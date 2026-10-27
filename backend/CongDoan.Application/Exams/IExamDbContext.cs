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
    Task InTransactionAsync(Func<Task> work, System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);
}

/// <summary>Reads and writes the question import spreadsheet (first worksheet, header row first).</summary>
public interface IQuestionTemplate
{
    IReadOnlyList<string[]> ReadXlsx(Stream xlsx);
    byte[] Xlsx(IEnumerable<string[]> rows);
}
