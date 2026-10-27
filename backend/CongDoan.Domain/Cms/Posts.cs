namespace CongDoan.Domain.Cms;

public enum PostStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Scheduled = 3,
    Published = 4,
    Archived = 5,
}

public sealed class Post
{
    private readonly List<PostTag> _tags = new();
    private readonly List<PostAttachment> _attachments = new();

    private Post() { }

    public Post(string title, string slug, bool slugIsManual, Guid? authorId, DateTime nowUtc)
    {
        Id = Guid.NewGuid();
        Title = title;
        Slug = slug;
        SlugIsManual = slugIsManual;
        AuthorId = authorId;
        CreatedUtc = nowUtc;
        UpdatedUtc = nowUtc;
        Status = PostStatus.Draft;
    }

    public Guid Id { get; private set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool SlugIsManual { get; set; }
    public string? Summary { get; set; }
    public string Body { get; set; } = "";
    public Guid? CoverMediaId { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsFeatured { get; set; }
    public string? SourceKey { get; set; }
    /// <summary>Accent-free lower-case text used by public search.</summary>
    public string SearchText { get; set; } = "";
    public PostStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public Guid? AuthorId { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public DateTime? ScheduledUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public IReadOnlyList<PostTag> Tags => _tags;
    public IReadOnlyList<PostAttachment> Attachments => _attachments;

    public bool IsPublic(DateTime nowUtc) => !IsDeleted && Status == PostStatus.Published && PublishedUtc <= nowUtc;

    public void SetTags(IEnumerable<Guid> tagIds)
    {
        var wanted = tagIds.Distinct().ToList();
        _tags.RemoveAll(t => !wanted.Contains(t.TagId));
        foreach (var id in wanted.Where(id => _tags.All(t => t.TagId != id))) _tags.Add(new PostTag(Id, id));
    }

    public void SetAttachments(IEnumerable<Guid> mediaIds)
    {
        _attachments.Clear();
        var order = 0;
        foreach (var id in mediaIds.Distinct()) _attachments.Add(new PostAttachment(Id, id, order++));
    }

    /// <summary>Applies a workflow move already authorised by the caller. Returns false if the move is not allowed.</summary>
    public bool TryTransition(PostStatus to, DateTime nowUtc, DateTime? scheduledUtc, string? note)
    {
        if (!PostWorkflow.IsAllowed(Status, to)) return false;
        if (to == PostStatus.Scheduled && (scheduledUtc is null || scheduledUtc <= nowUtc)) return false;
        Status = to;
        ReviewNote = note;
        ScheduledUtc = to == PostStatus.Scheduled ? scheduledUtc : null;
        if (to == PostStatus.Published) PublishedUtc ??= nowUtc;
        if (to == PostStatus.Scheduled) PublishedUtc = scheduledUtc;
        UpdatedUtc = nowUtc;
        return true;
    }

    /// <summary>Used by the scheduler: moves a due scheduled post to published (idempotent).</summary>
    public bool PublishIfDue(DateTime nowUtc)
    {
        if (Status != PostStatus.Scheduled || ScheduledUtc > nowUtc || IsDeleted) return false;
        Status = PostStatus.Published;
        PublishedUtc = ScheduledUtc;
        ScheduledUtc = null;
        UpdatedUtc = nowUtc;
        return true;
    }

    public void SoftDelete(DateTime nowUtc)
    {
        IsDeleted = true;
        DeletedUtc = nowUtc;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedUtc = null;
    }
}

public static class PostWorkflow
{
    private static readonly Dictionary<PostStatus, PostStatus[]> AllowedFrom = new()
    {
        [PostStatus.PendingReview] = new[] { PostStatus.Draft },
        [PostStatus.Draft] = new[] { PostStatus.PendingReview, PostStatus.Approved, PostStatus.Scheduled, PostStatus.Published, PostStatus.Archived },
        [PostStatus.Approved] = new[] { PostStatus.PendingReview },
        [PostStatus.Scheduled] = new[] { PostStatus.Draft, PostStatus.PendingReview, PostStatus.Approved },
        [PostStatus.Published] = new[] { PostStatus.Draft, PostStatus.PendingReview, PostStatus.Approved, PostStatus.Scheduled, PostStatus.Archived },
        [PostStatus.Archived] = new[] { PostStatus.Published },
    };

    public static bool IsAllowed(PostStatus from, PostStatus to) =>
        from != to && AllowedFrom.TryGetValue(to, out var froms) && froms.Contains(from);
}

public sealed class PostTag
{
    private PostTag() { }

    public PostTag(Guid postId, Guid tagId)
    {
        PostId = postId;
        TagId = tagId;
    }

    public Guid PostId { get; private set; }
    public Guid TagId { get; private set; }
}

public sealed class PostAttachment
{
    private PostAttachment() { }

    public PostAttachment(Guid postId, Guid mediaId, int sortOrder)
    {
        PostId = postId;
        MediaId = mediaId;
        SortOrder = sortOrder;
    }

    public Guid PostId { get; private set; }
    public Guid MediaId { get; private set; }
    public int SortOrder { get; private set; }
}

public sealed class Category
{
    private Category() { }

    public Category(string name, string slug)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
    }

    public Guid Id { get; private set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool SlugIsManual { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
}

public sealed class Tag
{
    private Tag() { }

    public Tag(string name, string slug)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
    }

    public Guid Id { get; private set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
