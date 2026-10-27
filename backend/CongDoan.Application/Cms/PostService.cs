using System.Text.RegularExpressions;
using CongDoan.Application.Identity;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record SavePost(string Title, string? Slug, string? Summary, string? Body, Guid? CoverMediaId, Guid? CategoryId,
    IReadOnlyList<string>? Tags, IReadOnlyList<Guid>? AttachmentIds, bool IsFeatured = false, string? RowVersion = null);

public sealed record TransitionPost(string To, DateTime? ScheduledUtc, string? Note);

public sealed record PostDto(Guid Id, string Title, string Slug, bool SlugIsManual, string? Summary, string Body, Guid? CoverMediaId,
    Guid? CategoryId, IReadOnlyList<string> Tags, IReadOnlyList<Guid> AttachmentIds, bool IsFeatured, string Status, string? ReviewNote,
    Guid? AuthorId, DateTime CreatedUtc, DateTime UpdatedUtc, DateTime? PublishedUtc, DateTime? ScheduledUtc, bool IsDeleted, string RowVersion);

public sealed record PostListItem(Guid Id, string Title, string Slug, string Status, Guid? CategoryId, Guid? AuthorId,
    DateTime UpdatedUtc, DateTime? PublishedUtc, bool IsFeatured, bool IsDeleted);

public sealed record PostFilter(int Page = 1, int PageSize = 20, string? Q = null, string? Status = null, Guid? CategoryId = null, bool Deleted = false, bool Mine = false);

public sealed partial class PostService
{
    private readonly ICmsDbContext _db;
    private readonly IContentSanitizer _sanitizer;
    private readonly IPermissionChecker _permissions;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IMediaUsageTracker _usages;
    private readonly ISlugHistory _history;

    public PostService(ICmsDbContext db, IContentSanitizer sanitizer, IPermissionChecker permissions, ICurrentUser user,
        IClock clock, IAuditWriter audit, IMediaUsageTracker usages, ISlugHistory history)
    {
        _db = db;
        _sanitizer = sanitizer;
        _permissions = permissions;
        _user = user;
        _clock = clock;
        _audit = audit;
        _usages = usages;
        _history = history;
    }

    public async Task<PagedResult<PostListItem>> ListAsync(PostFilter f, CancellationToken ct = default)
    {
        var page = new PageQuery(f.Page, f.PageSize, f.Q);
        var q = _db.Posts.AsNoTracking().Where(p => p.IsDeleted == f.Deleted);
        if (Enum.TryParse<PostStatus>(f.Status, true, out var st)) q = q.Where(p => p.Status == st);
        if (f.CategoryId is not null) q = q.Where(p => p.CategoryId == f.CategoryId);
        if (f.Mine) q = q.Where(p => p.AuthorId == _user.AdminId);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = Slug.SearchText(f.Q);
            q = q.Where(p => p.Title.Contains(f.Q) || p.SearchText.Contains(term));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.UpdatedUtc).Skip(page.Skip).Take(page.SafeSize)
            .Select(p => new PostListItem(p.Id, p.Title, p.Slug, p.Status.ToString(), p.CategoryId, p.AuthorId, p.UpdatedUtc, p.PublishedUtc, p.IsFeatured, p.IsDeleted))
            .ToListAsync(ct);
        return new PagedResult<PostListItem>(items, total, page.SafePage, page.SafeSize);
    }

    public async Task<PostDto> GetAsync(Guid id, CancellationToken ct = default) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    public async Task<PostDto> CreateAsync(SavePost cmd, CancellationToken ct = default)
    {
        Validate(cmd);
        var (slug, manual) = SlugRules.Resolve(cmd.Slug, cmd.Title);
        await EnsureSlugFreeAsync(slug, null, ct);
        var post = new Post(cmd.Title.Trim(), slug, manual, _user.AdminId, _clock.UtcNow);
        await ApplyAsync(post, cmd, ct);
        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);
        await AfterSaveAsync(post, ct);
        await _audit.WriteAsync("cms.post.create", "Post", post.Id.ToString(), new { post.Title, post.Slug }, ct);
        return await ToDtoAsync(post, ct);
    }

    public async Task<PostDto> UpdateAsync(Guid id, SavePost cmd, CancellationToken ct = default)
    {
        Validate(cmd);
        var post = await LoadAsync(id, ct);
        await EnsureCanEditAsync(post, ct);
        _db.ExpectRowVersion(post, RowVersions.Decode(cmd.RowVersion));

        var oldSlug = post.Slug;
        var (slug, manual) = string.IsNullOrWhiteSpace(cmd.Slug) && post.SlugIsManual
            ? (post.Slug, true)
            : SlugRules.Resolve(cmd.Slug, cmd.Title);
        if (slug != post.Slug) await EnsureSlugFreeAsync(slug, post.Id, ct);
        post.Title = cmd.Title.Trim();
        post.Slug = slug;
        post.SlugIsManual = manual;
        await ApplyAsync(post, cmd, ct);
        post.UpdatedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (oldSlug != slug) await _history.RecordAsync("post", oldSlug, slug, ct);
        await AfterSaveAsync(post, ct);
        await _audit.WriteAsync("cms.post.update", "Post", post.Id.ToString(), new { post.Title, post.Slug }, ct);
        return await ToDtoAsync(post, ct);
    }

    public async Task<PostDto> TransitionAsync(Guid id, TransitionPost cmd, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PostStatus>(cmd.To, true, out var to)) throw AppException.Validation("Trạng thái không hợp lệ.");
        var post = await LoadAsync(id, ct);
        await EnsureCanTransitionAsync(post, to, ct);
        var from = post.Status;
        if (!post.TryTransition(to, _clock.UtcNow, cmd.ScheduledUtc, cmd.Note))
            throw new AppException("invalid_transition",
                to == PostStatus.Scheduled ? "Cần chọn thời điểm xuất bản trong tương lai." : $"Không thể chuyển từ \"{Label(from)}\" sang \"{Label(to)}\".", 400);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.post.transition", "Post", post.Id.ToString(), new { from = from.ToString(), to = to.ToString(), cmd.ScheduledUtc, cmd.Note }, ct);
        return await ToDtoAsync(post, ct);
    }

    public async Task DeleteAsync(Guid id, bool restore, CancellationToken ct = default)
    {
        var post = await _db.Posts.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw AppException.NotFound();
        if (restore)
        {
            if (await _db.Posts.AnyAsync(p => p.Slug == post.Slug && p.Id != id && !p.IsDeleted, ct))
                throw AppException.Conflict("duplicate_slug", "Đường dẫn của bài đã được bài khác dùng; đổi đường dẫn trước khi khôi phục.");
            post.Restore();
        }
        else post.SoftDelete(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(restore ? "cms.post.restore" : "cms.post.delete", "Post", id.ToString(), new { post.Title }, ct);
    }

    private async Task EnsureCanEditAsync(Post post, CancellationToken ct)
    {
        var me = _user.AdminId ?? throw AppException.Forbidden();
        if (post.Status is PostStatus.Published or PostStatus.Scheduled)
        {
            if (!await _permissions.HasAsync(me, Permissions.CmsPostPublish, null, ct))
                throw AppException.Forbidden("Bài đã xuất bản chỉ người có quyền xuất bản mới sửa được.");
            return;
        }
        if (await _permissions.HasAsync(me, Permissions.CmsPostReview, null, ct)) return;
        if (post.AuthorId != me || post.Status != PostStatus.Draft)
            throw AppException.Forbidden("Bạn chỉ sửa được bài nháp của chính mình.");
    }

    private async Task EnsureCanTransitionAsync(Post post, PostStatus to, CancellationToken ct)
    {
        var me = _user.AdminId ?? throw AppException.Forbidden();
        var canEdit = await _permissions.HasAsync(me, Permissions.CmsPostEdit, null, ct);
        var canReview = await _permissions.HasAsync(me, Permissions.CmsPostReview, null, ct);
        var canPublish = await _permissions.HasAsync(me, Permissions.CmsPostPublish, null, ct);
        var isAuthor = post.AuthorId == me;
        var ok = to switch
        {
            PostStatus.PendingReview => canReview || (isAuthor && canEdit),
            PostStatus.Approved => canReview,
            PostStatus.Draft => post.Status is PostStatus.PendingReview ? canReview || isAuthor : canPublish,
            _ => canPublish,
        };
        if (!ok) throw AppException.Forbidden("Bạn không có quyền chuyển bài sang trạng thái này.");
    }

    private async Task ApplyAsync(Post post, SavePost cmd, CancellationToken ct)
    {
        post.Summary = cmd.Summary?.Trim();
        post.Body = _sanitizer.Sanitize(cmd.Body);
        post.CoverMediaId = cmd.CoverMediaId;
        post.IsFeatured = cmd.IsFeatured;
        if (cmd.CategoryId is { } cat && !await _db.Categories.AnyAsync(c => c.Id == cat && !c.IsDeleted, ct))
            throw AppException.Validation("Chuyên mục không tồn tại.");
        post.CategoryId = cmd.CategoryId;
        post.SetTags(await UpsertTagsAsync(cmd.Tags ?? Array.Empty<string>(), ct));
        post.SetAttachments(cmd.AttachmentIds ?? Array.Empty<Guid>());
        post.SearchText = Slug.SearchText($"{post.Title} {post.Summary} {StripTags(post.Body)}");
    }

    private async Task AfterSaveAsync(Post post, CancellationToken ct)
    {
        var ids = MediaRefs(post.Body).ToList();
        if (post.CoverMediaId is { } cover) ids.Add(cover);
        ids.AddRange(post.Attachments.Select(a => a.MediaId));
        await _usages.SetUsagesAsync("Post", post.Id, post.Title, ids, ct);
    }

    private async Task<List<Guid>> UpsertTagsAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var wanted = names.Select(n => n.Trim()).Where(n => n.Length > 0).DistinctBy(Slug.From).ToList();
        var slugs = wanted.Select(Slug.From).ToList();
        var existing = await _db.Tags.Where(t => slugs.Contains(t.Slug)).ToListAsync(ct);
        foreach (var name in wanted.Where(n => existing.All(t => t.Slug != Slug.From(n))))
        {
            var tag = new Tag(name, Slug.From(name));
            _db.Tags.Add(tag);
            existing.Add(tag);
        }
        return existing.Select(t => t.Id).ToList();
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? self, CancellationToken ct)
    {
        Task<bool> Taken(string s) => _db.Posts.AnyAsync(p => p.Slug == s && p.Id != self, ct);
        if (await Taken(slug)) throw await SlugRules.DuplicateAsync(slug, Taken);
    }

    private async Task<Post> LoadAsync(Guid id, CancellationToken ct) =>
        await _db.Posts.Include(p => p.Tags).Include(p => p.Attachments).SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw AppException.NotFound();

    private async Task<PostDto> ToDtoAsync(Post p, CancellationToken ct)
    {
        var tagIds = p.Tags.Select(t => t.TagId).ToList();
        var tags = await _db.Tags.AsNoTracking().Where(t => tagIds.Contains(t.Id)).Select(t => t.Name).ToListAsync(ct);
        return new PostDto(p.Id, p.Title, p.Slug, p.SlugIsManual, p.Summary, p.Body, p.CoverMediaId, p.CategoryId, tags,
            p.Attachments.OrderBy(a => a.SortOrder).Select(a => a.MediaId).ToList(), p.IsFeatured, p.Status.ToString(), p.ReviewNote,
            p.AuthorId, p.CreatedUtc, p.UpdatedUtc, p.PublishedUtc, p.ScheduledUtc, p.IsDeleted, RowVersions.Encode(p.RowVersion));
    }

    private static void Validate(SavePost cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Title)) throw AppException.Validation("Tiêu đề là bắt buộc.");
        if (cmd.Title.Length > 500) throw AppException.Validation("Tiêu đề tối đa 500 ký tự.");
    }

    public static IEnumerable<Guid> MediaRefs(string? html) =>
        MediaRef().Matches(html ?? "").Select(m => Guid.Parse(m.Groups[1].Value)).Distinct();

    public static string StripTags(string? html) => Tags().Replace(html ?? "", " ");

    private static string Label(PostStatus s) => s switch
    {
        PostStatus.Draft => "Nháp",
        PostStatus.PendingReview => "Chờ duyệt",
        PostStatus.Approved => "Đã duyệt",
        PostStatus.Scheduled => "Lên lịch",
        PostStatus.Published => "Đã xuất bản",
        _ => "Lưu trữ",
    };

    [GeneratedRegex(@"/media/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex MediaRef();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}

/// <summary>Remembers old slugs so renamed content keeps working through 301 redirects.</summary>
public interface ISlugHistory
{
    Task RecordAsync(string kind, string oldSlug, string newSlug, CancellationToken ct = default);
}
