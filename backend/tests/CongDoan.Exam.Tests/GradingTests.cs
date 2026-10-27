using System.Text.Json;
using CongDoan.Application.Exams;
using CongDoan.Domain.Exams;

namespace CongDoan.Exam.Tests;

public class GradingTests
{
    private static AttemptQuestion Q(Guid attempt, int order, decimal points, out Guid correct, out Guid wrong)
    {
        correct = Guid.NewGuid();
        wrong = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new[] { new SnapshotChoice(correct, "Đúng", true), new SnapshotChoice(wrong, "Sai", false) }, AttemptService.Json);
        return new AttemptQuestion(attempt, order, Guid.NewGuid(), Guid.NewGuid(), QuestionType.Single, "Câu " + order, null, points, json);
    }

    private static Answer A(Guid attempt, AttemptQuestion q, params Guid[] picks) =>
        new(attempt, q.Id) { SelectedJson = JsonSerializer.Serialize(picks, AttemptService.Json) };

    [Theory]
    [InlineData(2, 6.67, true, 2)]   // 2/3 → 6.666… rounds half away from zero to 6.67; pass mark 6.67 is inclusive
    [InlineData(2, 6.68, false, 2)]
    [InlineData(2, 6.7, true, 1)]    // 1 decimal → 6.7
    [InlineData(3, 10, true, 2)]
    [InlineData(0, 0, true, 2)]      // pass mark 0: everyone passes
    public void Rounding_And_PassScore_Boundaries(int correctAnswers, decimal passScore, bool expectedPass, int decimals)
    {
        var attempt = Guid.NewGuid();
        var qs = Enumerable.Range(1, 3).Select(i => Q(attempt, i, 1, out _, out _)).ToList();
        var keys = qs.Select(q => AttemptService.Choices(q)).ToList();
        var answers = qs.Take(correctAnswers).Select((q, i) => A(attempt, q, keys[i].Single(c => c.IsCorrect).Id)).ToList();

        var result = Grader.Grade(qs, answers, passScore, decimals);

        var expected = Math.Round(correctAnswers / 3m * 10, decimals, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, result.Score);
        Assert.Equal(expectedPass, result.Passed);
        Assert.Equal(correctAnswers, result.CorrectCount);
    }

    [Fact]
    public void Weighted_Points_Are_Respected()
    {
        var attempt = Guid.NewGuid();
        var heavy = Q(attempt, 1, 3, out var hc, out _);
        var light = Q(attempt, 2, 1, out _, out var lw);
        var result = Grader.Grade(new[] { heavy, light }, new[] { A(attempt, heavy, hc), A(attempt, light, lw) }, 5, 2);
        Assert.Equal(3m, result.Points);
        Assert.Equal(7.5m, result.Score);
    }

    private static Attempt Finished(int no, decimal score)
    {
        var a = new Attempt(Guid.NewGuid(), Guid.NewGuid(), no, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10), 10);
        a.Finish(AttemptStatus.Submitted, DateTime.UtcNow, 0, score, score >= 5, score);
        return a;
    }

    [Theory]
    [InlineData(ScoreRule.Highest, 8)]
    [InlineData(ScoreRule.Latest, 4)]
    [InlineData(ScoreRule.First, 6)]
    public void FinalScore_Highest_Latest_First(ScoreRule rule, decimal expected)
    {
        var attempts = new[] { Finished(1, 6), Finished(2, 8), Finished(3, 4) };
        Assert.Equal(expected, ResultPolicy.Final(rule, attempts)!.Score);
    }
}
