using System.Net;
using System.Text.Json;
using CongDoan.Application.Identity;
using CongDoan.Testing;

namespace CongDoan.Exam.Tests;

/// <summary>A started attempt with its client, question view and answer key (looked up through the admin API).</summary>
public sealed class AttemptFlow : IDisposable
{
    public TestClient Admin { get; init; } = null!;
    public TestClient Candidate { get; init; } = null!;
    public Guid ExamId { get; init; }
    public Guid AttemptId { get; init; }
    public string Writer { get; private set; } = "";
    public JsonElement View { get; init; }

    public static async Task<AttemptFlow> StartAsync(ApiFactory f, int questions = 3, object? exam = null)
    {
        var admin = await f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await admin.BankAsync(questions);
        var cand = await f.CandidateAsync(CmsHelpers.Unique("ts"));
        var examId = await admin.PublishedExamAsync(f, ids, new[] { cand.Id }, exam);
        var t = await f.LoginCandidateAsync(cand.UserName);
        var start = await TestClient.DataAsync(await t.PostAsync($"/api/thi/exams/{examId}/start"));
        var attemptId = start.GetProperty("attemptId").GetGuid();
        var view = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{attemptId}"));
        var flow = new AttemptFlow { Admin = admin, Candidate = t, ExamId = examId, AttemptId = attemptId, View = view };
        await flow.ClaimAsync();
        return flow;
    }

    public JsonElement Question(int index) => View.GetProperty("questions")[index];

    public Guid QuestionId(int index) => Question(index).GetProperty("id").GetGuid();

    /// <summary>Choice ids of question <paramref name="index"/>: (correct, wrong...). Bank questions have exactly one correct choice "Đúng n".</summary>
    public (Guid Correct, List<Guid> Wrong) Key(int index)
    {
        var choices = Question(index).GetProperty("choices").EnumerateArray().ToList();
        var correct = choices.Single(c => c.GetProperty("content").GetString()!.StartsWith("Đúng")).GetProperty("id").GetGuid();
        return (correct, choices.Select(c => c.GetProperty("id").GetGuid()).Where(id => id != correct).ToList());
    }

    public async Task<string> ClaimAsync(TestClient? c = null, bool force = false)
    {
        var res = await (c ?? Candidate).PostAsync($"/api/thi/attempts/{AttemptId}/claim", new { force });
        var data = await TestClient.DataAsync(res);
        var token = data.GetProperty("writerToken").GetString()!;
        if (c is null) Writer = token;
        return token;
    }

    public Task<HttpResponseMessage> SaveAsync(int index, IEnumerable<Guid> choiceIds, long seq, string? writer = null, TestClient? c = null, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["choiceIds"] = choiceIds.ToArray(), ["seq"] = seq };
        if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/thi/attempts/{AttemptId}/answers/{QuestionId(index)}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(body),
        };
        req.Headers.Add("X-Attempt-Writer", writer ?? Writer);
        return SendWithCsrf(c ?? Candidate, req);
    }

    private static async Task<HttpResponseMessage> SendWithCsrf(TestClient c, HttpRequestMessage req)
    {
        req.Headers.Add("X-XSRF-TOKEN", await c.CsrfAsync());
        return await c.Http.SendAsync(req);
    }

    public async Task<JsonElement> SubmitAsync() => await TestClient.DataAsync(await Candidate.PostAsync($"/api/thi/attempts/{AttemptId}/submit"));

    public void Dispose()
    {
        Admin.Dispose();
        Candidate.Dispose();
    }
}
