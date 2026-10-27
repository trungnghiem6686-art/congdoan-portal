namespace CongDoan.Domain.Cms;

public enum LayoutStatus
{
    Draft = 0,
    Published = 1,
    Superseded = 2,
}

/// <summary>
/// One version of a block layout for the homepage ("homepage") or a static page ("page:{id}").
/// At most one Draft and one Published per target (enforced by filtered unique indexes).
/// </summary>
public sealed class LayoutRevision
{
    public const string Homepage = "homepage";

    private LayoutRevision() { }

    public LayoutRevision(string target, string sectionsJson, Guid? createdBy, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        Target = target;
        SectionsJson = sectionsJson;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        Status = LayoutStatus.Draft;
    }

    public Guid Id { get; private set; }
    public string Target { get; private set; } = "";
    /// <summary>0 while a draft; assigned (max + 1) when published.</summary>
    public int Version { get; private set; }
    public LayoutStatus Status { get; private set; }
    public string SectionsJson { get; set; } = "[]";
    public string? Note { get; set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; set; }
    public Guid? PublishedBy { get; private set; }
    public DateTime? PublishedUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static string ForPage(Guid pageId) => $"page:{pageId}";

    public void MarkPublished(int version, Guid? by, DateTime nowUtc)
    {
        Status = LayoutStatus.Published;
        Version = version;
        PublishedBy = by;
        PublishedUtc = nowUtc;
    }

    public void MarkSuperseded() => Status = LayoutStatus.Superseded;
}
