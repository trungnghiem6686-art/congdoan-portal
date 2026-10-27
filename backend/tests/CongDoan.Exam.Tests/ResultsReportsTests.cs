using System.Net;
using System.Text;
using ClosedXML.Excel;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class ResultVisibilityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ResultVisibilityTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Result_Hidden_Before_Release()
    {
        var now = _f.Clock.UtcNow;
        using var a = await AttemptFlow.StartAsync(_f, questions: 2, exam: new { resultRelease = "AfterClose", closesAtUtc = now.AddHours(2) });
        await a.SaveAsync(0, new[] { a.Key(0).Correct }, 1);
        var submit = await a.SubmitAsync();
        Assert.False(submit.GetProperty("resultVisible").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, submit.GetProperty("score").ValueKind);

        var hidden = await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}/result"));
        Assert.False(hidden.GetProperty("visible").GetBoolean());
        Assert.DoesNotContain("score\":5", hidden.GetRawText());

        _f.Clock.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));
        var shown = await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}/result"));
        Assert.True(shown.GetProperty("visible").GetBoolean());
        Assert.Equal(5m, shown.GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task After_Approval_Needs_Admin_Approval()
    {
        using var a = await AttemptFlow.StartAsync(_f, exam: new { resultRelease = "AfterApproval" });
        await a.SubmitAsync();
        Assert.False((await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}/result"))).GetProperty("visible").GetBoolean());
        await TestClient.DataAsync(await a.Admin.PostAsync($"/api/admin/exams/{a.ExamId}/approve-results"));
        Assert.True((await TestClient.DataAsync(await a.Candidate.GetAsync($"/api/thi/attempts/{a.AttemptId}/result"))).GetProperty("visible").GetBoolean());
    }

    [Fact]
    public async Task Detail_And_Explanation_Need_Separate_Permission()
    {
        using var noDetail = await AttemptFlow.StartAsync(_f, exam: new { showAnswerDetail = false, showExplanation = false });
        await noDetail.SubmitAsync();
        var r1 = await TestClient.DataAsync(await noDetail.Candidate.GetAsync($"/api/thi/attempts/{noDetail.AttemptId}/result"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, r1.GetProperty("questions").ValueKind);

        using var detail = await AttemptFlow.StartAsync(_f, exam: new { showAnswerDetail = true, showExplanation = false });
        await detail.SaveAsync(0, new[] { detail.Key(0).Correct }, 1);
        await detail.SubmitAsync();
        var r2 = await TestClient.DataAsync(await detail.Candidate.GetAsync($"/api/thi/attempts/{detail.AttemptId}/result"));
        var q0 = r2.GetProperty("questions").EnumerateArray().Single(q => q.GetProperty("id").GetGuid() == detail.QuestionId(0));
        Assert.True(q0.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, q0.GetProperty("explanation").ValueKind);

        // Admin side: score needs exam.result.view, answer detail needs exam.result.detail.
        using var viewer = await _f.AdminWithRoleAsync(Roles.ReportViewer);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/admin/attempts/{detail.AttemptId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/admin/attempts/{detail.AttemptId}/detail")).StatusCode);
        var full = await TestClient.DataAsync(await detail.Admin.GetAsync($"/api/admin/attempts/{detail.AttemptId}/detail"));
        Assert.Contains("Giải thích", full.GetRawText());
    }

    [Fact]
    public async Task Candidate_Cannot_Read_Other_Result_By_Id()
    {
        using var a = await AttemptFlow.StartAsync(_f);
        await a.SubmitAsync();
        var other = await _f.CandidateAsync(CmsHelpers.Unique("x"));
        using var o = await _f.LoginCandidateAsync(other.UserName);
        Assert.Equal(HttpStatusCode.NotFound, (await o.GetAsync($"/api/thi/attempts/{a.AttemptId}/result")).StatusCode);
    }

    [Fact]
    public async Task Regrade_Keeps_Original_And_Reason()
    {
        using var a = await AttemptFlow.StartAsync(_f, questions: 2);
        await a.SaveAsync(0, new[] { a.Key(0).Correct }, 1);
        await a.SubmitAsync();

        var bad = await a.Admin.PostAsync($"/api/admin/attempts/{a.AttemptId}/regrade", new { score = 8, reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var res = await TestClient.DataAsync(await a.Admin.PostAsync($"/api/admin/attempts/{a.AttemptId}/regrade",
            new { score = 8, reason = "Câu 2 có hai đáp án đúng do lỗi đề" }));
        Assert.Equal(8m, res.GetProperty("score").GetDecimal());

        var detail = await TestClient.DataAsync(await a.Admin.GetAsync($"/api/admin/attempts/{a.AttemptId}"));
        var rev = detail.GetProperty("revisions")[0];
        Assert.Equal(5m, rev.GetProperty("oldScore").GetDecimal());
        Assert.Equal(8m, rev.GetProperty("newScore").GetDecimal());
        Assert.Equal("Câu 2 có hai đáp án đúng do lỗi đề", rev.GetProperty("reason").GetString());

        using var viewer = await _f.AdminWithRoleAsync(Roles.ReportViewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/admin/attempts/{a.AttemptId}/regrade", new { score = 9, reason = "x" })).StatusCode);
    }
}

public class ReportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ReportTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Report_Distinguishes_People_From_Attempts()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await admin.BankAsync(2);
        var c1 = await _f.CandidateAsync(CmsHelpers.Unique("r1"));
        var c2 = await _f.CandidateAsync(CmsHelpers.Unique("r2"));
        var c3 = await _f.CandidateAsync(CmsHelpers.Unique("r3"));
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { c1.Id, c2.Id, c3.Id }, new { maxAttempts = 3, scoreRule = "Highest" });

        async Task Take(Domain.Identity.Candidate c, int correct)
        {
            using var t = await _f.LoginCandidateAsync(c.UserName);
            var start = await TestClient.DataAsync(await t.PostAsync($"/api/thi/exams/{examId}/start"));
            var id = start.GetProperty("attemptId").GetGuid();
            var view = await TestClient.DataAsync(await t.GetAsync($"/api/thi/attempts/{id}"));
            var writer = (await TestClient.DataAsync(await t.PostAsync($"/api/thi/attempts/{id}/claim", new { force = true }))).GetProperty("writerToken").GetString();
            for (var i = 0; i < correct; i++)
            {
                var q = view.GetProperty("questions")[i];
                var choice = q.GetProperty("choices").EnumerateArray().Single(x => x.GetProperty("content").GetString()!.StartsWith("Đúng")).GetProperty("id").GetGuid();
                var req = new HttpRequestMessage(HttpMethod.Put, $"/api/thi/attempts/{id}/answers/{q.GetProperty("id").GetGuid()}")
                { Content = System.Net.Http.Json.JsonContent.Create(new { choiceIds = new[] { choice }, seq = i + 1 }) };
                req.Headers.Add("X-Attempt-Writer", writer);
                req.Headers.Add("X-XSRF-TOKEN", await t.CsrfAsync());
                (await t.Http.SendAsync(req)).EnsureSuccessStatusCode();
            }
            await TestClient.DataAsync(await t.PostAsync($"/api/thi/attempts/{id}/submit"));
        }

        await Take(c1, 0);
        await Take(c1, 2);
        await Take(c2, 0);

        var report = await TestClient.DataAsync(await admin.GetAsync($"/api/admin/reports/exams/{examId}"));
        var s = report.GetProperty("summary");
        Assert.Equal(3, s.GetProperty("assignedPeople").GetInt32());
        Assert.Equal(2, s.GetProperty("attemptedPeople").GetInt32());
        Assert.Equal(1, s.GetProperty("notAttemptedPeople").GetInt32());
        Assert.Equal(3, s.GetProperty("finishedAttempts").GetInt32());
        Assert.Equal(1, s.GetProperty("passedPeople").GetInt32());
        Assert.Equal(50m, s.GetProperty("passRatePercent").GetDecimal());
        Assert.Equal("attemptedPeople", s.GetProperty("passRateDenominator").GetString());
        var row1 = report.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("candidateId").GetGuid() == c1.Id);
        Assert.Equal(2, row1.GetProperty("attempts").GetInt32());
        Assert.Equal(10m, row1.GetProperty("finalScore").GetDecimal());
    }
}

public class ExportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ExportTests(ApiFactory f) => _f = f;

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+84 900", "'+84 900")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("\rx", "'\rx")]
    [InlineData("Nguyễn Văn A", "Nguyễn Văn A")]
    public void Export_Escapes_Formula_Prefixes(string input, string expected) => Assert.Equal(expected, CsvSanitizer.Escape(input));

    [Fact]
    public void Csv_Quotes_Commas_Quotes_And_Newlines() =>
        Assert.Equal("\"a,b\",\"c \"\"d\"\"\",\"e\nf\"", CsvSanitizer.Line(new[] { "a,b", "c \"d\"", "e\nf" }));

    [Fact]
    public async Task Export_Writes_AuditRow()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await admin.BankAsync(1);
        var evil = await _f.CandidateAsync("=cmd-" + Guid.NewGuid().ToString("N")[..6]);
        var examId = await admin.PublishedExamAsync(_f, ids, new[] { evil.Id });

        var csv = await admin.PostAsync($"/api/admin/reports/exams/{examId}/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.ToString());
        var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        Assert.Contains("'=cmd-", text);
        Assert.DoesNotContain(",=cmd-", text);

        var xlsx = await admin.PostAsync($"/api/admin/reports/exams/{examId}/export?format=xlsx");
        using var book = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(book.Worksheet(1).CellsUsed(), c => c.HasFormula);

        var audits = await _f.ScopedAsync(sp => sp.GetRequiredService<IAuditDbContext>().AuditLogs
            .CountAsync(a => a.Action == "report.export" && a.EntityId == examId.ToString()));
        Assert.Equal(2, audits);

        using var viewer = await _f.AdminWithRoleAsync(Roles.ReportViewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/admin/reports/exams/{examId}/export?format=csv")).StatusCode);
    }
}
