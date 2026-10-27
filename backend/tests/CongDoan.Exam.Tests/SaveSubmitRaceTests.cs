using CongDoan.Application.Exams;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class SaveSubmitRaceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public SaveSubmitRaceTests(ApiFactory f) => _f = f;

    /// <summary>
    /// First saves racing the submit: every answer row that exists once the attempt is graded must have been graded,
    /// i.e. no save may slip in after grading read the answers.
    /// </summary>
    [Fact]
    public async Task First_Saves_Racing_Submit_Leave_No_Ungraded_Answers()
    {
        const int attempts = 12, questions = 8;
        var flows = new List<AttemptFlow>();
        for (var i = 0; i < attempts; i++) flows.Add(await AttemptFlow.StartAsync(_f, questions));
        try
        {
            await Task.WhenAll(flows.Select(async (flow, n) =>
            {
                var saves = Enumerable.Range(0, questions).Select(q => flow.SaveAsync(q, new[] { flow.Key(q).Correct }, 1)).ToList();
                await Task.Delay(n % 4);
                var submit = flow.Candidate.PostAsync($"/api/thi/attempts/{flow.AttemptId}/submit");
                await Task.WhenAll(saves.Cast<Task>().Append(submit));
            }));

            var ids = flows.Select(f => f.AttemptId).ToList();
            var ungraded = await _f.ScopedAsync(sp => sp.GetRequiredService<IExamDbContext>().Answers.AsNoTracking()
                .CountAsync(a => ids.Contains(a.AttemptId) && a.AwardedPoints == null));
            var notGraded = await _f.ScopedAsync(sp => sp.GetRequiredService<IExamDbContext>().Attempts.AsNoTracking()
                .CountAsync(a => ids.Contains(a.Id) && a.Score == null));
            Assert.Equal(0, notGraded);
            Assert.Equal(0, ungraded);
        }
        finally
        {
            foreach (var f in flows) f.Dispose();
        }
    }
}
