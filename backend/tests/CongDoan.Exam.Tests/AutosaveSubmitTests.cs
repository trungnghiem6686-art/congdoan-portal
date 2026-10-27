using System.Net;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class AutosaveTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public AutosaveTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Stale_Seq_Does_Not_Overwrite_Newer_Answer()
    {
        using var a = await AttemptFlow.StartAsync(_f);
        var (correct, wrong) = a.Key(0);
        var newer = await TestClient.DataAsync(await a.SaveAsync(0, new[] { correct }, seq: 5));
        Assert.True(newer.GetProperty("accepted").GetBoolean());
        var stale = await TestClient.DataAsync(await a.SaveAsync(0, new[] { wrong[0] }, seq: 3));
        Assert.False(stale.GetProperty("accepted").GetBoolean());

        var view = await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}"));
        var saved = view.GetProperty("answers").EnumerateArray().Single(x => x.GetProperty("questionId").GetGuid() == a.QuestionId(0));
        Assert.Equal(correct, saved.GetProperty("choiceIds")[0].GetGuid());
        Assert.Equal(5, saved.GetProperty("seq").GetInt64());
    }

    [Fact]
    public async Task Choice_From_Another_Question_Rejected()
    {
        using var a = await AttemptFlow.StartAsync(_f);
        var res = await a.SaveAsync(0, new[] { a.Key(1).Correct }, seq: 1);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var two = await a.SaveAsync(0, a.Key(0).Wrong.Take(2), seq: 2);
        Assert.Equal(HttpStatusCode.BadRequest, two.StatusCode);
    }

    [Fact]
    public async Task Save_After_Submit_Rejected()
    {
        using var a = await AttemptFlow.StartAsync(_f);
        await a.SubmitAsync();
        var res = await a.SaveAsync(0, new[] { a.Key(0).Correct }, seq: 1);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("attempt_finished", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Save_After_Deadline_Rejected_Ignoring_Client_Timestamp()
    {
        using var a = await AttemptFlow.StartAsync(_f, exam: new { durationMinutes = 10 });
        Assert.True((await TestClient.DataAsync(await a.SaveAsync(0, new[] { a.Key(0).Correct }, seq: 1))).GetProperty("accepted").GetBoolean());
        var clientTime = _f.Clock.UtcNow.AddMinutes(1);
        _f.Clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));

        var late = await a.SaveAsync(1, new[] { a.Key(1).Correct }, seq: 2, extra: new { clientTimeUtc = clientTime });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("deadline_passed", await TestClient.ErrorCodeAsync(late));

        var view = await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}"));
        Assert.Equal("Expired", view.GetProperty("status").GetString());
        Assert.Single(view.GetProperty("answers").EnumerateArray());
    }

    [Fact]
    public async Task Second_Tab_Gets_ReadOnly_Conflict()
    {
        using var a = await AttemptFlow.StartAsync(_f);
        var tab1 = a.Writer;
        Assert.True((await TestClient.DataAsync(await a.SaveAsync(0, new[] { a.Key(0).Correct }, seq: 1, tab1))).GetProperty("accepted").GetBoolean());

        var claim2 = await a.Candidate.PostAsync($"/api/thi/attempts/{a.AttemptId}/claim", new { force = false });
        Assert.Equal(HttpStatusCode.Conflict, claim2.StatusCode);
        Assert.Equal("another_tab_active", await TestClient.ErrorCodeAsync(claim2));
        var readOnlySave = await a.SaveAsync(0, a.Key(0).Wrong.Take(1), seq: 2, writer: "tab-two-without-token");
        Assert.Equal("read_only", await TestClient.ErrorCodeAsync(readOnlySave));

        var tab2 = await a.ClaimAsync(a.Candidate, force: true);
        Assert.NotEqual(tab1, tab2);
        var old = await a.SaveAsync(0, a.Key(0).Wrong.Take(1), seq: 3, tab1);
        Assert.Equal(HttpStatusCode.Conflict, old.StatusCode);
        Assert.Equal("writer_superseded", await TestClient.ErrorCodeAsync(old));
        Assert.True((await TestClient.DataAsync(await a.SaveAsync(0, a.Key(0).Wrong.Take(1), seq: 4, tab2))).GetProperty("accepted").GetBoolean());
    }
}

public class SubmitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public SubmitTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Submit_Twice_Returns_Same_Result_One_Row()
    {
        using var a = await AttemptFlow.StartAsync(_f, questions: 3);
        await a.SaveAsync(0, new[] { a.Key(0).Correct }, 1);
        await a.SaveAsync(1, new[] { a.Key(1).Correct }, 2);
        await a.SaveAsync(2, a.Key(2).Wrong.Take(1), 3);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => a.Candidate.PostAsync($"/api/thi/attempts/{a.AttemptId}/submit")));
        var scores = new List<decimal>();
        foreach (var r in results) scores.Add((await TestClient.DataAsync(r)).GetProperty("score").GetDecimal());
        var again = await a.SubmitAsync();
        Assert.All(scores, s => Assert.Equal(6.67m, s));
        Assert.Equal(6.67m, again.GetProperty("score").GetDecimal());
        Assert.Equal(2, again.GetProperty("correctCount").GetInt32());

        var audits = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .CountAsync(x => x.Action == "exam.attempt.submit" && x.EntityId == a.AttemptId.ToString()));
        Assert.Equal(1, audits);
    }

    [Fact]
    public async Task Multiple_Choice_Partial_Selection_Scores_Zero()
    {
        using var admin = await _f.AdminWithRoleAsync(CongDoan.Application.Identity.Roles.ExamManager);
        var topic = CmsHelpers.Unique("Nhieu");
        var q = await admin.CreateQuestionAsync(ExamHelpers.Multiple("Chọn các đáp án đúng", topic, 0, 2));
        var cand = await _f.CandidateAsync(CmsHelpers.Unique("ts"));
        var examId = await admin.PublishedExamAsync(_f, new[] { q.GetProperty("id").GetGuid() }, new[] { cand.Id }, new { maxAttempts = 3 });
        using var t = await _f.LoginCandidateAsync(cand.UserName);

        async Task<decimal> Run(params string[] picks)
        {
            var start = await TestClient.DataAsync(await t.PostAsync($"/api/thi/exams/{examId}/start"));
            var id = start.GetProperty("attemptId").GetGuid();
            var view = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{id}"));
            var question = view.GetProperty("questions")[0];
            var chosen = question.GetProperty("choices").EnumerateArray().Where(c => picks.Contains(c.GetProperty("content").GetString())).Select(c => c.GetProperty("id").GetGuid()).ToArray();
            var writer = (await TestClient.DataAsync(await t.PostAsync($"/api/thi/attempts/{id}/claim", new { force = true }))).GetProperty("writerToken").GetString();
            var req = new HttpRequestMessage(HttpMethod.Put, $"/api/thi/attempts/{id}/answers/{question.GetProperty("id").GetGuid()}")
            { Content = System.Net.Http.Json.JsonContent.Create(new { choiceIds = chosen, seq = 1 }) };
            req.Headers.Add("X-Attempt-Writer", writer);
            req.Headers.Add("X-XSRF-TOKEN", await t.CsrfAsync());
            (await t.Http.SendAsync(req)).EnsureSuccessStatusCode();
            return (await TestClient.DataAsync(await t.PostAsync($"/api/thi/attempts/{id}/submit"))).GetProperty("score").GetDecimal();
        }

        Assert.Equal(0m, await Run("A1"));
        Assert.Equal(0m, await Run("A1", "C1", "D1"));
        Assert.Equal(10m, await Run("A1", "C1"));
    }

    [Fact]
    public async Task Blank_Answer_Scores_Zero()
    {
        using var a = await AttemptFlow.StartAsync(_f, questions: 2);
        var result = await a.SubmitAsync();
        Assert.Equal(0m, result.GetProperty("score").GetDecimal());
        Assert.False(result.GetProperty("passed").GetBoolean());
    }
}
