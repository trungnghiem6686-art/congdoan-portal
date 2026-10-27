using System.Net.Mail;
using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record SubmitContact(string? Name, string? Email, string? Phone, string? Subject, string? Message, string? Website);

public sealed record ContactDto(Guid Id, string Name, string Email, string? Phone, string? Subject, string Message, DateTime CreatedUtc,
    string Status, string? HandlerNote, DateTime? HandledUtc);

public sealed record HandleContact(string Status, string? Note);

public sealed class ContactService
{
    private readonly ICmsDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly IAuditWriter _audit;

    public ContactService(ICmsDbContext db, IClock clock, ICurrentUser user, IAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _user = user;
        _audit = audit;
    }

    /// <summary>Saves the request; the caller reports success only after this returns the stored id.</summary>
    public async Task<Guid> SubmitAsync(SubmitContact cmd, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(cmd.Website)) throw new AppException("spam_detected", "Không gửi được liên hệ.", 400);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 200) errors.Add("họ tên");
        if (!IsEmail(cmd.Email)) errors.Add("email hợp lệ");
        if (string.IsNullOrWhiteSpace(cmd.Message) || cmd.Message.Length > 5000) errors.Add("nội dung (tối đa 5000 ký tự)");
        if (cmd.Phone is { Length: > 30 }) errors.Add("số điện thoại hợp lệ");
        if (errors.Count > 0) throw AppException.Validation("Vui lòng nhập " + string.Join(", ", errors) + ".");

        var row = new ContactRequest(cmd.Name!.Trim(), cmd.Email!.Trim(), cmd.Phone?.Trim(), cmd.Subject?.Trim(), cmd.Message!.Trim(), _user.IpAddress, _clock.UtcNow);
        _db.ContactRequests.Add(row);
        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task<PagedResult<ContactDto>> ListAsync(PageQuery q, string? status, CancellationToken ct = default)
    {
        var query = _db.ContactRequests.AsNoTracking();
        if (Enum.TryParse<ContactStatus>(status, true, out var st)) query = query.Where(c => c.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Q)) query = query.Where(c => c.Name.Contains(q.Q) || c.Email.Contains(q.Q) || c.Message.Contains(q.Q));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedUtc).Skip(q.Skip).Take(q.SafeSize)
            .Select(c => new ContactDto(c.Id, c.Name, c.Email, c.Phone, c.Subject, c.Message, c.CreatedUtc, c.Status.ToString(), c.HandlerNote, c.HandledUtc))
            .ToListAsync(ct);
        return new PagedResult<ContactDto>(items, total, q.SafePage, q.SafeSize);
    }

    public async Task HandleAsync(Guid id, HandleContact cmd, CancellationToken ct = default)
    {
        var c = await _db.ContactRequests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (!Enum.TryParse<ContactStatus>(cmd.Status, true, out var st)) throw AppException.Validation("Trạng thái không hợp lệ.");
        c.Status = st;
        c.HandlerNote = cmd.Note?.Trim();
        c.HandledBy = _user.AdminId;
        c.HandledUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.contact.handle", "ContactRequest", id.ToString(), new { cmd.Status }, ct);
    }

    private static bool IsEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 200 && MailAddress.TryCreate(email.Trim(), out var a) && a.Address == email.Trim() && email.Contains('.');
}

public sealed record SearchResult(string Type, string Title, string Url, string? Snippet, DateTime? Date);

public sealed record SearchQuery(string? Q, string? Type = null, int Page = 1, int PageSize = 10);

/// <summary>Accent-insensitive public search over published content only (posts, visible pages, documents, albums).</summary>
public sealed class SearchService
{
    private readonly ICmsDbContext _db;
    private readonly PublicPostService _posts;

    public SearchService(ICmsDbContext db, PublicPostService posts)
    {
        _db = db;
        _posts = posts;
    }

    public async Task<PagedResult<SearchResult>> SearchAsync(SearchQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, Math.Min(q.PageSize, 50));
        var terms = Slug.SearchText(q.Q).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(8).ToList();
        if (terms.Count == 0) return new PagedResult<SearchResult>(Array.Empty<SearchResult>(), 0, 1, page.SafeSize);
        var take = page.Skip + page.SafeSize;
        bool Want(string type) => string.IsNullOrWhiteSpace(q.Type) || q.Type == type;
        var results = new List<SearchResult>();
        var total = 0;

        if (Want("post"))
        {
            var posts = _posts.Visible();
            foreach (var t in terms) posts = posts.Where(p => p.SearchText.Contains(t));
            total += await posts.CountAsync(ct);
            results.AddRange(await posts.OrderByDescending(p => p.PublishedUtc).Take(take)
                .Select(p => new SearchResult("post", p.Title, PublicPaths.Post + p.Slug, p.Summary, p.PublishedUtc)).ToListAsync(ct));
        }
        if (Want("page"))
        {
            var pages = _db.Pages.AsNoTracking().Where(p => p.IsVisible && !p.IsDeleted);
            foreach (var t in terms) pages = pages.Where(p => p.SearchText.Contains(t));
            total += await pages.CountAsync(ct);
            results.AddRange(await pages.OrderByDescending(p => p.UpdatedUtc).Take(take)
                .Select(p => new SearchResult("page", p.Title, PublicPaths.Page + p.Slug, p.SeoDescription, p.UpdatedUtc)).ToListAsync(ct));
        }
        if (Want("document"))
        {
            var docs = _db.Documents.AsNoTracking().Where(d => d.IsVisible && !d.IsDeleted);
            foreach (var t in terms) docs = docs.Where(d => d.SearchText.Contains(t));
            total += await docs.CountAsync(ct);
            var list = await docs.OrderByDescending(d => d.IssuedDate).Take(take).ToListAsync(ct);
            results.AddRange(list.Select(d => new SearchResult("document", $"{d.Number} – {d.Title}", PublicPaths.Document + d.Slug, d.Summary,
                d.IssuedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))));
        }
        if (Want("album"))
        {
            var albums = _db.Albums.AsNoTracking().Where(a => a.IsVisible && !a.IsDeleted);
            foreach (var t in terms) albums = albums.Where(a => a.SearchText.Contains(t));
            total += await albums.CountAsync(ct);
            results.AddRange(await albums.OrderByDescending(a => a.CreatedUtc).Take(take)
                .Select(a => new SearchResult("album", a.Title, PublicPaths.Album + a.Slug, a.Description, a.CreatedUtc)).ToListAsync(ct));
        }
        var pageItems = results.OrderByDescending(r => r.Date).Skip(page.Skip).Take(page.SafeSize)
            .Select(r => r with { Snippet = Snip(r.Snippet) }).ToList();
        return new PagedResult<SearchResult>(pageItems, total, page.SafePage, page.SafeSize);
    }

    private static string? Snip(string? s) => s is null ? null : s.Length <= 240 ? s : s[..240].TrimEnd() + "…";
}

public sealed record DocumentQuery(int Page = 1, int PageSize = 20, string? Q = null, string? Number = null, string? DocType = null,
    string? Issuer = null, DateOnly? From = null, DateOnly? To = null);

public sealed record PublicCategory(Guid Id, string Name, string Slug, string? Description, Guid? ParentId, int SortOrder);

public sealed class PublicContentService
{
    private readonly ICmsDbContext _db;
    private readonly AlbumService _albums;

    public PublicContentService(ICmsDbContext db, AlbumService albums)
    {
        _db = db;
        _albums = albums;
    }

    public async Task<IReadOnlyList<PublicCategory>> CategoriesAsync(CancellationToken ct = default) =>
        await _db.Categories.AsNoTracking().Where(c => c.IsVisible && !c.IsDeleted).OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PublicCategory(c.Id, c.Name, c.Slug, c.Description, c.ParentId, c.SortOrder)).ToListAsync(ct);

    public async Task<PagedResult<DocumentDto>> DocumentsAsync(DocumentQuery q, CancellationToken ct = default)
    {
        var page = new PageQuery(q.Page, Math.Min(q.PageSize, 100));
        var docs = _db.Documents.AsNoTracking().Where(d => d.IsVisible && !d.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q.Number)) docs = docs.Where(d => d.Number.Contains(q.Number.Trim()));
        if (!string.IsNullOrWhiteSpace(q.DocType)) docs = docs.Where(d => d.DocType == q.DocType);
        if (!string.IsNullOrWhiteSpace(q.Issuer)) docs = docs.Where(d => d.Issuer != null && d.Issuer.Contains(q.Issuer));
        if (q.From is { } from) docs = docs.Where(d => d.IssuedDate >= from);
        if (q.To is { } to) docs = docs.Where(d => d.IssuedDate <= to);
        foreach (var t in Slug.SearchText(q.Q).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8)) docs = docs.Where(d => d.SearchText.Contains(t));
        var total = await docs.CountAsync(ct);
        var items = await docs.OrderByDescending(d => d.IssuedDate).ThenBy(d => d.Number).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<DocumentDto>(items.Select(DocumentService.ToDto).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<DocumentDto?> DocumentAsync(string slug, CancellationToken ct = default)
    {
        var d = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug && x.IsVisible && !x.IsDeleted, ct);
        return d is null ? null : DocumentService.ToDto(d);
    }

    public async Task<IReadOnlyList<string>> DocumentTypesAsync(CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking().Where(d => d.IsVisible && !d.IsDeleted).Select(d => d.DocType).Distinct().OrderBy(t => t).ToListAsync(ct);

    public async Task<PagedResult<AlbumDto>> AlbumsAsync(int pageNo, int size, string? kind, CancellationToken ct = default)
    {
        var page = new PageQuery(pageNo, Math.Min(size, 50));
        var albums = _db.Albums.AsNoTracking().Where(a => a.IsVisible && !a.IsDeleted);
        if (Enum.TryParse<AlbumKind>(kind, true, out var k)) albums = albums.Where(a => a.Kind == k);
        var total = await albums.CountAsync(ct);
        var items = await albums.OrderBy(a => a.SortOrder).ThenByDescending(a => a.CreatedUtc).Skip(page.Skip).Take(page.SafeSize).ToListAsync(ct);
        return new PagedResult<AlbumDto>(items.Select(a => AlbumService.ToDto(a, Array.Empty<AlbumItemDto>())).ToList(), total, page.SafePage, page.SafeSize);
    }

    public async Task<AlbumDto?> AlbumAsync(string slug, CancellationToken ct = default)
    {
        var a = await _db.Albums.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug && x.IsVisible && !x.IsDeleted, ct);
        return a is null ? null : AlbumService.ToDto(a, await _albums.ItemsAsync(a.Id, ct));
    }
}
