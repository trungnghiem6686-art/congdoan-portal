using System.Net;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Exam.Tests;

public class QuestionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public QuestionTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Rejects_No_Correct_Choice()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var res = await c.PostAsync("/api/admin/questions", new
        {
            type = "Single", topic = "Luật", difficulty = "Easy", content = "Câu không có đáp án đúng", points = 1,
            choices = new[] { new { content = "A", isCorrect = false }, new { content = "B", isCorrect = false } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("đáp án đúng", await res.Content.ReadAsStringAsync());

        var twoForSingle = await c.PostAsync("/api/admin/questions", new
        {
            type = "Single", topic = "Luật", difficulty = "Easy", content = "Một đáp án mà chọn hai", points = 1,
            choices = new[] { new { content = "A", isCorrect = true }, new { content = "B", isCorrect = true } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, twoForSingle.StatusCode);
    }

    [Fact]
    public async Task Rejects_Duplicate_Choices()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var res = await c.PostAsync("/api/admin/questions", new
        {
            type = "Multiple", topic = "Luật", difficulty = "Easy", content = "Trùng phương án", points = 1,
            choices = new[] { new { content = "Đúng luật", isCorrect = true }, new { content = "  đúng LUẬT ", isCorrect = false }, new { content = "Khác", isCorrect = false } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("trùng", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Import_With_One_Bad_Row_Writes_Nothing_And_Reports_Row()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var topic = CmsHelpers.Unique("NhapLieu");
        var csv = "chu_de,muc_do,loai,noi_dung,dap_an_a,dap_an_b,dap_an_c,dap_an_d,dap_an_dung,giai_thich,diem\n"
                  + $"{topic},de,mot_dap_an,Câu 1?,A,B,C,D,A,Vì A,1\n"
                  + $"{topic},trung_binh,nhieu_dap_an,\"Câu 2, có dấu phẩy?\",A,B,C,D,\"A,C\",,2\n"
                  + $"{topic},kho,mot_dap_an,Câu 3 lỗi?,A,B,,,E,,1\n"
                  + $"{topic},de,dung_sai,Câu 4?,Đúng,Sai,,,B,,1\n";

        var preview = await TestClient.DataAsync(await c.PostAsync("/api/admin/questions/import/preview", new { csv }));
        Assert.Equal(4, preview.GetProperty("rowCount").GetInt32());
        var errors = preview.GetProperty("errors").EnumerateArray().ToList();
        Assert.Single(errors);
        Assert.Equal(4, errors[0].GetProperty("row").GetInt32());

        var commit = await c.PostAsync("/api/admin/questions/import/commit", new { previewId = preview.GetProperty("previewId").GetGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, commit.StatusCode);
        var list = await TestClient.DataAsync(await c.GetAsync($"/api/admin/questions?topic={Uri.EscapeDataString(topic)}&pageSize=50"));
        Assert.Equal(0, list.GetProperty("total").GetInt32());

        var fixedCsv = csv.Replace($"{topic},kho,mot_dap_an,Câu 3 lỗi?,A,B,,,E,,1\n", $"{topic},kho,mot_dap_an,Câu 3?,A,B,,,B,,1\n");
        var ok = await TestClient.DataAsync(await c.PostAsync("/api/admin/questions/import/preview", new { csv = fixedCsv }));
        Assert.Empty(ok.GetProperty("errors").EnumerateArray());
        var done = await TestClient.DataAsync(await c.PostAsync("/api/admin/questions/import/commit", new { previewId = ok.GetProperty("previewId").GetGuid() }));
        Assert.Equal(4, done.GetProperty("created").GetInt32());
        var again = await c.PostAsync("/api/admin/questions/import/commit", new { previewId = ok.GetProperty("previewId").GetGuid() });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        list = await TestClient.DataAsync(await c.GetAsync($"/api/admin/questions?topic={Uri.EscapeDataString(topic)}&pageSize=50"));
        Assert.Equal(4, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Import_Xlsx_Uses_Same_Rules()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var topic = CmsHelpers.Unique("Excel");
        var bytes = await _f.ScopedAsync(sp => Task.FromResult(sp.GetRequiredService<IQuestionTemplate>().Xlsx(new[]
        {
            new[] { topic, "de", "mot_dap_an", "Câu Excel 1?", "A", "B", "", "", "B", "", "1" },
            new[] { topic, "de", "mot_dap_an", "", "A", "B", "", "", "B", "", "1" },
        })));
        var res = await c.PostAsync("/api/admin/questions/import/preview",
            Files.Form(bytes, "cau-hoi.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        var preview = await TestClient.DataAsync(res);
        Assert.Equal(3, preview.GetProperty("errors")[0].GetProperty("row").GetInt32());
    }

    [Fact]
    public async Task Edit_Creates_New_Version()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var q = await c.CreateQuestionAsync(ExamHelpers.Single("Phiên bản 1"));
        var id = q.GetProperty("id").GetGuid();
        Assert.Equal(1, q.GetProperty("versionNo").GetInt32());

        var edited = await TestClient.DataAsync(await c.PutAsync($"/api/admin/questions/{id}",
            new Dictionary<string, object?>(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(
                System.Text.Json.JsonSerializer.Serialize(ExamHelpers.Single("Phiên bản 2")))!)
            { ["rowVersion"] = q.GetProperty("rowVersion").GetString() }));
        Assert.Equal(2, edited.GetProperty("versionNo").GetInt32());
        Assert.Equal("Draft", edited.GetProperty("status").GetString());
        Assert.Equal("Phiên bản 2", edited.GetProperty("content").GetString());

        var detail = await TestClient.DataAsync(await c.GetAsync($"/api/admin/questions/{id}"));
        var versions = detail.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("versionNo").GetInt32()).ToList();
        Assert.Equal(new[] { 2, 1 }, versions);
    }

    [Fact]
    public async Task Unapproved_Question_Not_Usable_In_Exam()
    {
        using var c = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var draft = await c.CreateQuestionAsync(ExamHelpers.Single("Chưa duyệt"), approve: false);
        var approved = await c.CreateQuestionAsync(ExamHelpers.Single("Đã duyệt"));

        var ex = await Assert.ThrowsAsync<CongDoan.Application.Shared.AppException>(() => _f.ScopedAsync(sp =>
            sp.GetRequiredService<QuestionService>().EnsureUsableAsync(new[] { draft.GetProperty("id").GetGuid(), approved.GetProperty("id").GetGuid() })));
        Assert.Equal("question_not_approved", ex.Code);
        await _f.ScopedAsync(sp => sp.GetRequiredService<QuestionService>().EnsureUsableAsync(new[] { approved.GetProperty("id").GetGuid() }));
    }

    [Fact]
    public async Task Approve_Needs_Permission()
    {
        using var mgr = await _f.AdminWithRoleAsync(Roles.ExamManager);
        var q = await mgr.CreateQuestionAsync(ExamHelpers.Single("Cần duyệt"), approve: false);
        using var viewer = await _f.AdminWithRoleAsync(Roles.ReportViewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/admin/questions/{q.GetProperty("id").GetGuid()}/approve")).StatusCode);
    }
}
