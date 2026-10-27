namespace CongDoan.Application.Identity;

public sealed record PermissionDefinition(string Code, string Module, string Name);

/// <summary>Core permission codes. Modules add theirs through <see cref="IPermissionProvider"/>.</summary>
public static class Permissions
{
    public const string UserManage = "identity.user.manage";
    public const string RoleManage = "identity.role.manage";
    public const string CandidateView = "identity.candidate.view";
    public const string CandidateManage = "identity.candidate.manage";
    public const string OrgUnitManage = "identity.orgunit.manage";

    public const string CmsPostCreate = "cms.post.create";
    public const string CmsPostEdit = "cms.post.edit";
    public const string CmsPostReview = "cms.post.review";
    public const string CmsPostPublish = "cms.post.publish";
    public const string CmsPostDelete = "cms.post.delete";
    public const string CmsMediaUpload = "cms.media.upload";
    public const string CmsMediaManage = "cms.media.manage";
    public const string CmsMenuManage = "cms.menu.manage";
    public const string CmsBannerManage = "cms.banner.manage";
    public const string CmsPageManage = "cms.page.manage";
    public const string CmsHomepageManage = "cms.homepage.manage";
    public const string CmsSettingsManage = "cms.settings.manage";
    public const string CmsDocumentManage = "cms.document.manage";
    public const string CmsAlbumManage = "cms.album.manage";
    public const string CmsContactView = "cms.contact.view";

    public const string ExamQuestionManage = "exam.question.manage";
    public const string ExamQuestionApprove = "exam.question.approve";
    public const string ExamManage = "exam.exam.manage";
    public const string ExamPublish = "exam.exam.publish";
    public const string ExamResultView = "exam.result.view";
    public const string ExamResultDetail = "exam.result.detail";
    public const string ExamRegrade = "exam.result.regrade";

    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<PermissionDefinition> Core = new PermissionDefinition[]
    {
        new(UserManage, "identity", "Quản lý tài khoản quản trị"),
        new(RoleManage, "identity", "Quản lý vai trò và quyền"),
        new(CandidateView, "identity", "Xem tài khoản thí sinh"),
        new(CandidateManage, "identity", "Quản lý tài khoản thí sinh"),
        new(OrgUnitManage, "identity", "Quản lý đơn vị và nhóm"),
        new(CmsPostCreate, "cms", "Tạo bài viết"),
        new(CmsPostEdit, "cms", "Sửa bài viết"),
        new(CmsPostReview, "cms", "Duyệt bài viết"),
        new(CmsPostPublish, "cms", "Xuất bản bài viết"),
        new(CmsPostDelete, "cms", "Xóa/khôi phục bài viết"),
        new(CmsMediaUpload, "cms", "Tải lên tệp"),
        new(CmsMediaManage, "cms", "Quản lý thư viện tệp"),
        new(CmsMenuManage, "cms", "Quản lý menu"),
        new(CmsBannerManage, "cms", "Quản lý banner"),
        new(CmsPageManage, "cms", "Quản lý trang tĩnh"),
        new(CmsHomepageManage, "cms", "Dựng và xuất bản trang chủ"),
        new(CmsSettingsManage, "cms", "Cấu hình website, footer"),
        new(CmsDocumentManage, "cms", "Quản lý văn bản"),
        new(CmsAlbumManage, "cms", "Quản lý thư viện ảnh/video"),
        new(CmsContactView, "cms", "Xem liên hệ"),
        new(ExamQuestionManage, "exam", "Quản lý ngân hàng câu hỏi"),
        new(ExamQuestionApprove, "exam", "Duyệt câu hỏi"),
        new(ExamManage, "exam", "Quản lý đề và kỳ thi"),
        new(ExamPublish, "exam", "Công bố kỳ thi"),
        new(ExamResultView, "exam", "Xem kết quả thi"),
        new(ExamResultDetail, "exam", "Xem chi tiết bài làm và lời giải"),
        new(ExamRegrade, "exam", "Chấm lại"),
        new(ReportView, "report", "Xem báo cáo"),
        new(ReportExport, "report", "Xuất báo cáo"),
        new(AuditView, "audit", "Xem nhật ký hệ thống"),
    };
}

/// <summary>Lets an add-on module (chatbot, member profiles, reports) contribute permissions without editing the core list.</summary>
public interface IPermissionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}

public static class Roles
{
    public const string SystemAdmin = "system_admin";
    public const string ContentAdmin = "content_admin";
    public const string Editor = "editor";
    public const string Reviewer = "reviewer";
    public const string ExamManager = "exam_manager";
    public const string ReportViewer = "report_viewer";

    public sealed record RoleSeed(string Code, string Name, string[] Permissions);

    public static IReadOnlyList<RoleSeed> Defaults(IEnumerable<string> allPermissions)
    {
        var all = allPermissions.ToArray();
        return new RoleSeed[]
        {
            new(SystemAdmin, "Quản trị hệ thống", all),
            new(ContentAdmin, "Quản trị nội dung", all.Where(p => p.StartsWith("cms.")).Append(Permissions.AuditView).ToArray()),
            new(Editor, "Biên tập viên", new[] { Permissions.CmsPostCreate, Permissions.CmsPostEdit, Permissions.CmsMediaUpload }),
            new(Reviewer, "Người duyệt", new[] { Permissions.CmsPostEdit, Permissions.CmsPostReview, Permissions.CmsPostPublish, Permissions.CmsMediaUpload }),
            new(ExamManager, "Quản lý kỳ thi", all.Where(p => p.StartsWith("exam."))
                .Concat(new[] { Permissions.CandidateView, Permissions.CandidateManage, Permissions.ReportView, Permissions.ReportExport }).ToArray()),
            new(ReportViewer, "Người xem báo cáo", new[] { Permissions.ReportView, Permissions.ExamResultView }),
        };
    }
}
