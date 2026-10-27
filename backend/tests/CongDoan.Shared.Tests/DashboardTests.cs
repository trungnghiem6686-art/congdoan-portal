using CongDoan.Application.Cms;
using CongDoan.Application.Exams;
using CongDoan.Application.Identity;
using CongDoan.Domain.Cms;
using CongDoan.Domain.Exams;
using CongDoan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CongDoan.Shared.Tests;

public class DashboardTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;

    public DashboardTests(ApiFactory f) => _f = f;

    [Fact]
    public async Task Dashboard_Counts_Match_Database()
    {
        using var admin = await _f.AdminWithRoleAsync(Roles.SystemAdmin);
        var p1 = await admin.CreatePostAsync(CmsHelpers.Unique("Đã đăng"));
        await admin.PublishAsync(p1.GetProperty("id").GetGuid());
        var p2 = await admin.CreatePostAsync(CmsHelpers.Unique("Chờ duyệt"));
        await admin.PostAsync($"/api/admin/posts/{p2.GetProperty("id").GetGuid()}/transition", new { to = "PendingReview" });
        await _f.CandidateAsync(CmsHelpers.Unique("dash"));
        using var anon = _f.CreateTestClient();
        await anon.PostAsync("/api/public/contact", new { name = "A", email = "a@example.com", message = "x" });

        var d = await TestClient.DataAsync(await admin.GetAsync("/api/admin/dashboard"));
        var expected = await _f.ScopedAsync(async sp =>
        {
            var cms = sp.GetRequiredService<ICmsDbContext>();
            var ids = sp.GetRequiredService<IIdentityDbContext>();
            var exam = sp.GetRequiredService<IExamDbContext>();
            return new
            {
                published = await cms.Posts.CountAsync(p => p.Status == PostStatus.Published && !p.IsDeleted),
                pending = await cms.Posts.CountAsync(p => p.Status == PostStatus.PendingReview && !p.IsDeleted),
                contacts = await cms.ContactRequests.CountAsync(c => c.Status == ContactStatus.New),
                candidates = await ids.Candidates.CountAsync(c => !c.IsDeleted),
                questions = await exam.Questions.CountAsync(q => q.Status == QuestionStatus.Approved && !q.IsDeleted),
            };
        });
        Assert.Equal(expected.published, d.GetProperty("publishedPosts").GetInt32());
        Assert.Equal(expected.pending, d.GetProperty("pendingReviewPosts").GetInt32());
        Assert.Equal(expected.contacts, d.GetProperty("newContacts").GetInt32());
        Assert.Equal(expected.candidates, d.GetProperty("candidates").GetInt32());
        Assert.Equal(expected.questions, d.GetProperty("approvedQuestions").GetInt32());
        Assert.True(d.GetProperty("pendingReviewPosts").GetInt32() >= 1);
    }
}
