using System.Net;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Domain.Exams;
using CongDoan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class ExamSetupTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public ExamSetupTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Publish_Fails_When_Bank_Has_Fewer_Questions_Than_Matrix()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (topic, _) = await c.BankAsync(3, "Easy");
        var exam = await c.CreateExamAsync(_f, new { questionSource = "Random" });
        var id = exam.GetProperty("id").GetGuid();
        await TestClient.DataAsync(await c.PutAsync($"/api/admin/exams/{id}/matrix", new { rules = new[] { new { topic, difficulty = "Easy", count = 5 } } }));
        var cand = await _f.CandidateAsync(CmsHelpers.Unique("ts"));
        await c.PutAsync($"/api/admin/exams/{id}/assignments", new { add = new[] { new { targetType = "Candidate", targetId = cand.Id } } });

        var check = await TestClient.DataAsync(await c.PostAsync($"/api/admin/exams/{id}/check"));
        Assert.False(check.GetProperty("ok").GetBoolean());
        Assert.Contains("3", check.GetProperty("problems")[0].GetString());

        var publish = await c.PostAsync($"/api/admin/exams/{id}/publish");
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        Assert.Equal("exam_not_ready", await TestClient.ErrorCodeAsync(publish));

        await TestClient.DataAsync(await c.PutAsync($"/api/admin/exams/{id}/matrix", new { rules = new[] { new { topic, difficulty = "Easy", count = 3 } } }));
        var ok = await TestClient.DataAsync(await c.PostAsync($"/api/admin/exams/{id}/publish"));
        Assert.Equal("Published", ok.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Draft_Questions_Block_Manual_Exam()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var draft = await c.CreateQuestionAsync(ExamHelpers.Single("Chưa duyệt " + Guid.NewGuid()), approve: false);
        var exam = await c.CreateExamAsync(_f);
        var res = await c.PutAsync($"/api/admin/exams/{exam.GetProperty("id").GetGuid()}/questions", new { questionIds = new[] { draft.GetProperty("id").GetGuid() } });
        Assert.Equal("question_not_approved", await TestClient.ErrorCodeAsync(res));
    }

    [Fact]
    public async Task Random_Draw_Has_No_Duplicate_Questions()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var topic = CmsHelpers.Unique("Ngau");
        await c.BankAsync(6, "Easy", topic);
        await c.BankAsync(4, "Hard", topic);
        var exam = await c.CreateExamAsync(_f, new { questionSource = "Random" });
        var id = exam.GetProperty("id").GetGuid();
        // Overlapping rules: "any difficulty" can pick the same questions as the "Easy" rule.
        await TestClient.DataAsync(await c.PutAsync($"/api/admin/exams/{id}/matrix", new
        {
            rules = new object[] { new { topic, difficulty = "Easy", count = 4 }, new { topic, count = 6 } },
        }));

        for (var i = 0; i < 20; i++)
        {
            var drawn = await _f.ScopedAsync(sp => sp.GetRequiredService<ExamService>().DrawAsync(id, new Random(i)));
            Assert.Equal(10, drawn.Count);
            Assert.Equal(drawn.Count, drawn.Distinct().Count());
        }
    }

    [Fact]
    public async Task Exam_With_Results_Cannot_Be_Deleted_Only_Archived()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await c.BankAsync(2);
        var cand = await _f.CandidateAsync(CmsHelpers.Unique("ts"));
        var examId = await c.PublishedExamAsync(_f, ids, new[] { cand.Id });

        // A finished attempt (the attempt API arrives with the exam runner).
        await _f.ScopedAsync(async sp =>
        {
            var db = sp.GetRequiredService<IExamDbContext>();
            var attempt = new Attempt(examId, cand.Id, 1, _f.Clock.UtcNow, _f.Clock.UtcNow.AddMinutes(30), 10);
            attempt.Finish(AttemptStatus.Submitted, _f.Clock.UtcNow, 5, 5m, true);
            db.Attempts.Add(attempt);
            await db.SaveChangesAsync();
        });

        var del = await c.DeleteAsync($"/api/admin/exams/{examId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Equal("exam_has_results", await TestClient.ErrorCodeAsync(del));
        var archived = await TestClient.DataAsync(await c.PostAsync($"/api/admin/exams/{examId}/archive"));
        Assert.Equal("Archived", archived.GetProperty("status").GetString());

        var draftExam = await c.CreateExamAsync(_f);
        Assert.Equal(HttpStatusCode.OK, (await c.DeleteAsync($"/api/admin/exams/{draftExam.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Assignment_By_Group_Expands_To_Members()
    {
        using var sys = await _f.AdminWithRoleAsync(Roles.SystemAdmin);
        var a = await _f.CandidateAsync(CmsHelpers.Unique("ga"));
        var b = await _f.CandidateAsync(CmsHelpers.Unique("gb"));
        var outsider = await _f.CandidateAsync(CmsHelpers.Unique("go"));
        var group = await TestClient.DataAsync(await sys.PostAsync("/api/admin/candidate-groups", new { name = CmsHelpers.Unique("Nhóm") }), HttpStatusCode.Created);
        var gid = group.GetProperty("id").GetGuid();
        await TestClient.DataAsync(await sys.PutAsync($"/api/admin/candidate-groups/{gid}/members", new { add = new[] { a.Id, b.Id } }));

        var exam = await sys.CreateExamAsync(_f);
        var id = exam.GetProperty("id").GetGuid();
        await TestClient.DataAsync(await sys.PutAsync($"/api/admin/exams/{id}/assignments", new { add = new[] { new { targetType = "Group", targetId = gid } } }));

        var expanded = await TestClient.DataAsync(await sys.GetAsync($"/api/admin/exams/{id}/candidates?pageSize=100"));
        var ids = expanded.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(a.Id, ids);
        Assert.Contains(b.Id, ids);
        Assert.DoesNotContain(outsider.Id, ids);

        Assert.True(await _f.ScopedAsync(sp => sp.GetRequiredService<ExamService>().IsAssignedAsync(id, a.Id)));
        Assert.False(await _f.ScopedAsync(sp => sp.GetRequiredService<ExamService>().IsAssignedAsync(id, outsider.Id)));
    }

    [Fact]
    public async Task Invalid_Window_Rejected()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var res = await c.PostAsync("/api/admin/exams", new
        {
            title = "Sai giờ", opensAtUtc = _f.Clock.UtcNow.AddDays(2), closesAtUtc = _f.Clock.UtcNow.AddDays(1),
            durationMinutes = 30, maxAttempts = 1, scoreRule = "Highest", passScore = 5, resultRelease = "Immediately", questionSource = "Manual",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Candidate_Sees_Only_Assigned_Published_Exams()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var (_, ids) = await c.BankAsync(1);
        var me = await _f.CandidateAsync(CmsHelpers.Unique("me"));
        var other = await _f.CandidateAsync(CmsHelpers.Unique("other"));
        var mine = await c.PublishedExamAsync(_f, ids, new[] { me.Id });
        var notMine = await c.PublishedExamAsync(_f, ids, new[] { other.Id });

        using var t = await _f.LoginCandidateAsync(me.UserName);
        var list = await TestClient.DataAsync(await t.GetAsync("/api/thi/exams"));
        var examIds = list.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(mine, examIds);
        Assert.DoesNotContain(notMine, examIds);
        Assert.Equal(2, list.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == mine).GetProperty("attemptsRemaining").GetInt32());
    }
}
