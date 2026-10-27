using System.Net;
using System.Text.Json;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Domain.Exams;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class StartAttemptTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public StartAttemptTests(ApiFactory f) => _f = f;

    private async Task<(TestClient Admin, List<Guid> Questions, Domain.Identity.Candidate Candidate)> SetupAsync(int questions = 4)
    {
        var admin = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await admin.BankAsync(questions);
        var cand = await _f.CandidateAsync(CmsHelpers.Unique("ts"));
        return (admin, ids, cand);
    }

    private static Task<HttpResponseMessage> StartAsync(TestClient c, Guid examId) => c.PostAsync($"/api/thi/exams/{examId}/start");

    [Fact]
    public async Task Not_Assigned_Returns_403()
    {
        var (admin, ids, cand) = await SetupAsync();
        var other = await _f.CandidateAsync(CmsHelpers.Unique("other"));
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id });
        using var t = await _f.LoginCandidateAsync(other.UserName);

        var res = await StartAsync(t, examId);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("not_assigned", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Before_Open_And_After_Close_Rejected()
    {
        var (admin, ids, cand) = await SetupAsync();
        var now = _f.Clock.UtcNow;
        var future = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { opensAtUtc = now.AddHours(1), closesAtUtc = now.AddHours(3) });
        var past = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { opensAtUtc = now.AddHours(-3), closesAtUtc = now.AddMinutes(-1) });
        using var t = await _f.LoginCandidateAsync(cand.UserName);

        Assert.Equal("exam_not_open", await TestClient.ErrorCodeAsync(await StartAsync(t, future)));
        Assert.Equal("exam_closed", await TestClient.ErrorCodeAsync(await StartAsync(t, past)));
    }

    [Fact]
    public async Task Over_Max_Attempts_Rejected()
    {
        var (admin, ids, cand) = await SetupAsync();
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { maxAttempts = 1 });
        using var t = await _f.LoginCandidateAsync(cand.UserName);
        var attemptId = (await TestClient.DataAsync(await StartAsync(t, examId))).GetProperty("attemptId").GetGuid();
        await _f.ScopedAsync(async sp =>
        {
            var db = sp.GetRequiredService<IExamDbContext>();
            var a = await db.Attempts.SingleAsync(x => x.Id == attemptId);
            a.Finish(AttemptStatus.Submitted, _f.Clock.UtcNow, 0, 0, false);
            await db.SaveChangesAsync();
        });

        var res = await StartAsync(t, examId);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("max_attempts_reached", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Two_Parallel_Starts_Create_One_Attempt()
    {
        var (admin, ids, cand) = await SetupAsync();
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { maxAttempts = 3 });
        using var t = await _f.LoginCandidateAsync(cand.UserName);

        var responses = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => StartAsync(t, examId)));
        var ok = new List<Guid>();
        foreach (var r in responses)
        {
            if (r.StatusCode == HttpStatusCode.OK) ok.Add((await TestClient.DataAsync(r)).GetProperty("attemptId").GetGuid());
            else Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        }
        Assert.NotEmpty(ok);
        Assert.Single(ok.Distinct());
        var count = await _f.ScopedAsync(sp => sp.GetRequiredService<IExamDbContext>().Attempts.CountAsync(a => a.ExamId == examId && a.CandidateId == cand.Id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Deadline_Is_Min_Of_Start_Plus_Duration_And_Close()
    {
        var (admin, ids, cand) = await SetupAsync();
        var now = _f.Clock.UtcNow;
        var roomy = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { durationMinutes = 30, closesAtUtc = now.AddDays(1) });
        var tight = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { durationMinutes = 30, closesAtUtc = now.AddMinutes(10) });
        using var t = await _f.LoginCandidateAsync(cand.UserName);

        var a = await TestClient.DataAsync(await StartAsync(t, roomy));
        Assert.Equal(now.AddMinutes(30), a.GetProperty("deadlineUtc").GetDateTime().ToUniversalTime());
        var b = await TestClient.DataAsync(await StartAsync(t, tight));
        Assert.Equal(now.AddMinutes(10), b.GetProperty("deadlineUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Reload_Keeps_Order()
    {
        var (admin, ids, cand) = await SetupAsync(8);
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id }, new { shuffleQuestions = true, shuffleChoices = true });
        using var t = await _f.LoginCandidateAsync(cand.UserName);
        var first = await TestClient.DataAsync(await StartAsync(t, examId));
        var attemptId = first.GetProperty("attemptId").GetGuid();

        static string Order(JsonElement a) => string.Join("|", a.GetProperty("questions").EnumerateArray()
            .Select(q => q.GetProperty("id").GetGuid() + ":" + string.Join(",", q.GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()))));

        var view1 = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{attemptId}"));
        var again = await TestClient.DataAsync(await StartAsync(t, examId));
        Assert.True(again.GetProperty("resumed").GetBoolean());
        Assert.Equal(attemptId, again.GetProperty("attemptId").GetGuid());
        var view2 = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{attemptId}"));
        Assert.Equal(Order(view1), Order(view2));
        Assert.Equal(8, view1.GetProperty("questions").GetArrayLength());
    }

    [Fact]
    public async Task Payload_Contains_No_Correct_Flag_Or_Explanation()
    {
        var (admin, ids, cand) = await SetupAsync();
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id });
        using var t = await _f.LoginCandidateAsync(cand.UserName);
        var start = await StartAsync(t, examId);
        var startJson = await start.Content.ReadAsStringAsync();
        var attemptId = JsonDocument.Parse(startJson).RootElement.GetProperty("data").GetProperty("attemptId").GetGuid();
        var view = await t.GetAsync($"/api/thi/attempts/{attemptId}");
        var json = await view.Content.ReadAsStringAsync();

        foreach (var payload in new[] { startJson, json })
        {
            foreach (var forbidden in new[] { "isCorrect", "correct", "explanation", "Giải thích", "answerKey", "points\":null" })
                Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("no-store", view.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Other_Candidate_Cannot_Read_Attempt()
    {
        var (admin, ids, cand) = await SetupAsync();
        var spy = await _f.CandidateAsync(CmsHelpers.Unique("spy"));
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id, spy.Id });
        using var t = await _f.LoginCandidateAsync(cand.UserName);
        var attemptId = (await TestClient.DataAsync(await StartAsync(t, examId))).GetProperty("attemptId").GetGuid();
        using var s = await _f.LoginCandidateAsync(spy.UserName);
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/thi/attempts/{attemptId}")).StatusCode);
    }

    [Fact]
    public async Task Editing_Bank_After_Start_Does_Not_Change_Attempt()
    {
        var (admin, ids, cand) = await SetupAsync(1);
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { cand.Id });
        using var t = await _f.LoginCandidateAsync(cand.UserName);
        var attemptId = (await TestClient.DataAsync(await StartAsync(t, examId))).GetProperty("attemptId").GetGuid();
        var before = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{attemptId}"));

        var q = await TestClient.DataAsync(await admin.GetAsync($"/api/admin/questions/{ids[0]}"));
        await TestClient.DataAsync(await admin.PutAsync($"/api/admin/questions/{ids[0]}", new
        {
            type = "Single", topic = q.GetProperty("topic").GetString(), difficulty = "Medium", content = "NỘI DUNG ĐÃ SỬA", points = 5,
            choices = new[] { new { content = "X mới", isCorrect = true }, new { content = "Y mới", isCorrect = false } },
            rowVersion = q.GetProperty("rowVersion").GetString(),
        }));

        var after = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{attemptId}"));
        Assert.Equal(before.GetProperty("questions").GetRawText(), after.GetProperty("questions").GetRawText());
        Assert.DoesNotContain("NỘI DUNG ĐÃ SỬA", after.GetRawText());
    }
}
