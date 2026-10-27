using System.Text.Json;
using CongDoan.Domain.Exams;

namespace CongDoan.Application.Exams;

public sealed record GradeResult(decimal Points, decimal MaxPoints, decimal Score, bool Passed, int CorrectCount, IReadOnlyDictionary<Guid, decimal> Awarded);

/// <summary>Grades from the attempt snapshot only. A question scores only if the selected set equals the correct set exactly.</summary>
public static class Grader
{
    public static GradeResult Grade(IReadOnlyCollection<AttemptQuestion> questions, IEnumerable<Answer> answers, decimal passScore, int decimals)
    {
        var byQuestion = answers.ToDictionary(a => a.AttemptQuestionId);
        var awarded = new Dictionary<Guid, decimal>();
        decimal points = 0, max = 0;
        var correctCount = 0;
        foreach (var q in questions)
        {
            max += q.Points;
            var key = AttemptService.Choices(q).Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
            var picked = byQuestion.TryGetValue(q.Id, out var a) ? AttemptService.Selected(a).ToHashSet() : new HashSet<Guid>();
            var right = picked.Count > 0 && picked.SetEquals(key);
            awarded[q.Id] = right ? q.Points : 0;
            if (!right) continue;
            points += q.Points;
            correctCount++;
        }
        var score = max == 0 ? 0 : Math.Round(points / max * 10, decimals, MidpointRounding.AwayFromZero);
        return new GradeResult(points, max, score, score >= passScore, correctCount, awarded);
    }
}

public sealed record SaveAnswer(IReadOnlyList<Guid>? ChoiceIds, long Seq, bool Flagged = false);

public sealed record SaveResult(bool Accepted, long Seq, DateTime ServerNowUtc, string? Reason = null);

public sealed record ClaimResult(string WriterToken, DateTime DeadlineUtc, DateTime ServerNowUtc);

public sealed record SubmitResult(Guid AttemptId, string Status, bool ResultVisible, decimal? Score, bool? Passed, int? CorrectCount, int QuestionCount);
