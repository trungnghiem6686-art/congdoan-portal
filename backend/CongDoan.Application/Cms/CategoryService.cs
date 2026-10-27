using CongDoan.Application.Shared;
using CongDoan.Domain.Cms;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Cms;

public sealed record SaveCategory(string Name, string? Slug, string? Description, Guid? ParentId, int SortOrder = 0, bool IsVisible = true, string? RowVersion = null);

public sealed record CategoryDto(Guid Id, string Name, string Slug, bool SlugIsManual, string? Description, Guid? ParentId,
    int SortOrder, bool IsVisible, bool IsDeleted, string RowVersion);

public sealed record TagDto(Guid Id, string Name, string Slug, int PostCount);

public sealed class CategoryService
{
    private readonly ICmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly ISlugHistory _history;

    public CategoryService(ICmsDbContext db, IAuditWriter audit, ISlugHistory history)
    {
        _db = db;
        _audit = audit;
        _history = history;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool deleted = false, CancellationToken ct = default) =>
        (await _db.Categories.AsNoTracking().Where(c => c.IsDeleted == deleted).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<CategoryDto> CreateAsync(SaveCategory cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw AppException.Validation("Tên chuyên mục là bắt buộc.");
        var (slug, manual) = SlugRules.Resolve(cmd.Slug, cmd.Name);
        await EnsureSlugFreeAsync(slug, null, ct);
        await EnsureParentAsync(null, cmd.ParentId, ct);
        var c = new Category(cmd.Name.Trim(), slug) { SlugIsManual = manual };
        Apply(c, cmd);
        _db.Categories.Add(c);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("cms.category.create", "Category", c.Id.ToString(), new { c.Name, c.Slug }, ct);
        return ToDto(c);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategory cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw AppException.Validation("Tên chuyên mục là bắt buộc.");
        var c = await _db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        if (cmd.RowVersion is not null) _db.ExpectRowVersion(c, RowVersions.Decode(cmd.RowVersion));
        var oldSlug = c.Slug;
        var (slug, manual) = string.IsNullOrWhiteSpace(cmd.Slug) && c.SlugIsManual ? (c.Slug, true) : SlugRules.Resolve(cmd.Slug, cmd.Name);
        if (slug != c.Slug) await EnsureSlugFreeAsync(slug, id, ct);
        await EnsureParentAsync(id, cmd.ParentId, ct);
        c.Name = cmd.Name.Trim();
        c.Slug = slug;
        c.SlugIsManual = manual;
        Apply(c, cmd);
        await _db.SaveChangesAsync(ct);
        if (oldSlug != slug) await _history.RecordAsync("category", oldSlug, slug, ct);
        await _audit.WriteAsync("cms.category.update", "Category", id.ToString(), new { c.Name, c.Slug }, ct);
        return ToDto(c);
    }

    public async Task SetDeletedAsync(Guid id, bool deleted, CancellationToken ct = default)
    {
        var c = await _db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound();
        c.IsDeleted = deleted;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(deleted ? "cms.category.delete" : "cms.category.restore", "Category", id.ToString(), null, ct);
    }

    public async Task<IReadOnlyList<TagDto>> TagsAsync(CancellationToken ct = default) =>
        await _db.Tags.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Slug, _db.Posts.Count(p => !p.IsDeleted && p.Tags.Any(x => x.TagId == t.Id))))
            .ToListAsync(ct);

    private static void Apply(Category c, SaveCategory cmd)
    {
        c.Description = cmd.Description?.Trim();
        c.ParentId = cmd.ParentId;
        c.SortOrder = cmd.SortOrder;
        c.IsVisible = cmd.IsVisible;
    }

    private async Task EnsureParentAsync(Guid? self, Guid? parentId, CancellationToken ct)
    {
        if (parentId is null) return;
        var parents = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, ct);
        if (!parents.ContainsKey(parentId.Value)) throw AppException.Validation("Chuyên mục cha không tồn tại.");
        for (Guid? p = parentId; p is not null; p = parents.GetValueOrDefault(p.Value))
            if (p == self) throw AppException.Validation("Không thể chọn chuyên mục con làm chuyên mục cha.");
    }

    private async Task EnsureSlugFreeAsync(string slug, Guid? self, CancellationToken ct)
    {
        Task<bool> Taken(string s) => _db.Categories.AnyAsync(c => c.Slug == s && c.Id != self, ct);
        if (await Taken(slug)) throw await SlugRules.DuplicateAsync(slug, Taken);
    }

    private static CategoryDto ToDto(Category c) =>
        new(c.Id, c.Name, c.Slug, c.SlugIsManual, c.Description, c.ParentId, c.SortOrder, c.IsVisible, c.IsDeleted, RowVersions.Encode(c.RowVersion));
}
