using CongDoan.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Exams;

public partial interface IExamDbContext
{
    DbSet<Exam> Exams { get; }
    DbSet<ExamQuestion> ExamQuestions { get; }
    DbSet<ExamMatrixRule> ExamMatrixRules { get; }
    DbSet<ExamAssignment> ExamAssignments { get; }
    DbSet<ExamTemplate> ExamTemplates { get; }
    DbSet<Attempt> Attempts { get; }
    DbSet<AttemptQuestion> AttemptQuestions { get; }
    DbSet<Answer> Answers { get; }
    DbSet<GradeRevision> GradeRevisions { get; }
    DbSet<AttemptEvent> AttemptEvents { get; }
}
