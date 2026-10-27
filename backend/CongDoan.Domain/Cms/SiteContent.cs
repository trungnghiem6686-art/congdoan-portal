namespace CongDoan.Domain.Cms;

/// <summary>Common shape for hideable, soft-deletable, orderable CMS items.</summary>
public abstract class CmsItem
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public bool IsVisible { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
}

public sealed class Menu
{
    private Menu() { }

    public Menu(string code, string name)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; set; } = "";
}

public enum MenuLinkType
{
    /// <summary>Heading / landing page listing its children.</summary>
    None = 0,
    Category = 1,
    Page = 2,
    Post = 3,
    Url = 4,
}

public sealed class MenuItem : CmsItem
{
    public Guid MenuId { get; set; }
    public Guid? ParentId { get; set; }
    public string Label { get; set; } = "";
    /// <summary>Root URL segment: the item is reachable at "/{Slug}".</summary>
    public string Slug { get; set; } = "";
    public bool SlugIsManual { get; set; }
    public MenuLinkType LinkType { get; set; }
    public Guid? TargetId { get; set; }
    public string? Url { get; set; }
    public bool OpenInNewTab { get; set; }
}

public sealed class Banner : CmsItem
{
    public string Title { get; set; } = "";
    public string Position { get; set; } = "home_top";
    public Guid? ImageMediaId { get; set; }
    public string? LinkUrl { get; set; }
    public string? Description { get; set; }
    public DateTime? StartsUtc { get; set; }
    public DateTime? EndsUtc { get; set; }

    public bool IsLive(DateTime nowUtc) =>
        IsVisible && !IsDeleted && (StartsUtc is null || StartsUtc <= nowUtc) && (EndsUtc is null || EndsUtc > nowUtc);
}

/// <summary>Official document (văn bản): number, type, issuer and dates are searchable.</summary>
public sealed class Document : CmsItem
{
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string DocType { get; set; } = "";
    public string? Issuer { get; set; }
    public DateOnly IssuedDate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string? Summary { get; set; }
    public Guid? FileMediaId { get; set; }
    public string SearchText { get; set; } = "";
    public string? SourceKey { get; set; }
}

public enum AlbumKind
{
    Photo = 1,
    Video = 2,
}

public sealed class Album : CmsItem
{
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public AlbumKind Kind { get; set; }
    public string? Description { get; set; }
    public Guid? CoverMediaId { get; set; }
    public DateTime? EventDate { get; set; }
    public string SearchText { get; set; } = "";
}

public sealed class AlbumItem
{
    private AlbumItem() { }

    public AlbumItem(Guid albumId, Guid? mediaId, string? embedUrl, string? caption, int sortOrder)
    {
        Id = Guid.NewGuid();
        AlbumId = albumId;
        MediaId = mediaId;
        EmbedUrl = embedUrl;
        Caption = caption;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid AlbumId { get; private set; }
    public Guid? MediaId { get; private set; }
    public string? EmbedUrl { get; private set; }
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Static page: rich-text body and/or blocks built with the homepage block builder.</summary>
public sealed class Page : CmsItem
{
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool SlugIsManual { get; set; }
    public string Body { get; set; } = "";
    public string? SeoDescription { get; set; }
    public string SearchText { get; set; } = "";
}

public sealed class SiteSetting
{
    private SiteSetting() { }

    public SiteSetting(string key, string json)
    {
        Key = key;
        Json = json;
    }

    public string Key { get; private set; } = "";
    public string Json { get; set; } = "{}";
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Old public path that permanently moved (renamed slug or imported legacy URL).</summary>
public sealed class Redirect
{
    private Redirect() { }

    public Redirect(string oldPath, string newPath, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        OldPath = oldPath;
        NewPath = newPath;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public string OldPath { get; private set; } = "";
    public string NewPath { get; set; } = "";
    public int StatusCode { get; private set; } = 301;
    public DateTime CreatedUtc { get; private set; }
    public int Hits { get; set; }
}

public enum ContactStatus
{
    New = 0,
    Handled = 1,
    Spam = 2,
}

public sealed class ContactRequest
{
    private ContactRequest() { }

    public ContactRequest(string name, string email, string? phone, string? subject, string message, string? ipAddress, DateTime createdUtc)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        Phone = phone;
        Subject = subject;
        Message = message;
        IpAddress = ipAddress;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string? Phone { get; private set; }
    public string? Subject { get; private set; }
    public string Message { get; private set; } = "";
    public string? IpAddress { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public ContactStatus Status { get; set; }
    public string? HandlerNote { get; set; }
    public Guid? HandledBy { get; set; }
    public DateTime? HandledUtc { get; set; }
}
