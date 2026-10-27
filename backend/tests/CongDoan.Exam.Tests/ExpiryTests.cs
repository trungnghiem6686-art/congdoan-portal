using CongDoan.Application.Exams;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class ExpiryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ExpiryTests(ApiFactory f) => _f = f;

    private Task<int> SweepAsync() => _f.ScopedAsync(sp => sp.GetRequiredService<ExpirySweeper>().RunOnceAsync());

    private Task<CongDoan.Domain.Exams.Attempt> AttemptAsync(Guid id) =>
        _f.ScopedAsync(sp => sp.GetRequiredService<IExamDbContext>().Attempts.AsNoTracking().SingleAsync(a => a.Id == id));

    [Fact]
    public async Task Expired_Attempt_Closed_And_Graded_Without_Browser()
    {
        using var a = await AttemptFlow.StartAsync(_f, questions: 2, exam: new { durationMinutes = 15 });
        await a.SaveAsync(0, new[] { a.Key(0).Correct }, 1);
        a.Candidate.Dispose(); // the browser is gone

        _f.Clock.Advance(TimeSpan.FromMinutes(14));
        await SweepAsync();
        Assert.Equal(CongDoan.Domain.Exams.AttemptStatus.InProgress, (await AttemptAsync(a.AttemptId)).Status);

        _f.Clock.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        Assert.True(await SweepAsync() >= 1);
        var attempt = await AttemptAsync(a.AttemptId);
        Assert.Equal(CongDoan.Domain.Exams.AttemptStatus.Expired, attempt.Status);
        Assert.Equal(5m, attempt.Score);
        Assert.Equal(1, attempt.CorrectCount);
    }

    [Fact]
    public async Task Worker_Restart_Mid_Exam_Does_Not_Extend_Deadline()
    {
        using var a = await AttemptFlow.StartAsync(_f, exam: new { durationMinutes = 20 });
        var deadline = (await AttemptAsync(a.AttemptId)).DeadlineUtc;

        _f.Clock.Advance(TimeSpan.FromMinutes(12));
        await SweepAsync();
        // "Restart": a fresh scope/worker instance and the candidate reloading the page.
        var resumed = await TestClient.DataAsync(await a.Candidate.PostAsync($"/api/thi/exams/{a.ExamId}/start"));
        Assert.True(resumed.GetProperty("resumed").GetBoolean());
        Assert.Equal(deadline, resumed.GetProperty("deadlineUtc").GetDateTime().ToUniversalTime());
        var view = await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}"));
        Assert.Equal(deadline, view.GetProperty("deadlineUtc").GetDateTime().ToUniversalTime());

        _f.Clock.Advance(TimeSpan.FromMinutes(8).Add(TimeSpan.FromSeconds(1)));
        await SweepAsync();
        Assert.Equal(CongDoan.Domain.Exams.AttemptStatus.Expired, (await AttemptAsync(a.AttemptId)).Status);
    }

    [Fact]
    public async Task Expired_Then_Submit_Is_Idempotent_Noop()
    {
        using var a = await AttemptFlow.StartAsync(_f, questions: 2, exam: new { durationMinutes = 5 });
        await a.SaveAsync(0, new[] { a.Key(0).Correct }, 1);
        await a.SaveAsync(1, new[] { a.Key(1).Correct }, 2);
        _f.Clock.Advance(TimeSpan.FromMinutes(6));
        await SweepAsync();

        var submit = await a.SubmitAsync();
        Assert.Equal("Expired", submit.GetProperty("status").GetString());
        Assert.Equal(10m, submit.GetProperty("score").GetDecimal());
        await SweepAsync();
        var audits = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .CountAsync(x => x.Action == "exam.attempt.submit" && x.EntityId == a.AttemptId.ToString()));
        Assert.Equal(1, audits);
    }
}
