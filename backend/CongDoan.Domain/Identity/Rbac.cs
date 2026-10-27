namespace CongDoan.Domain.Identity;

public sealed class Role
{
    private readonly List<RolePermission> _permissions = new();

    private Role() { }

    public Role(string code, string name, bool isSystem)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        IsSystem = isSystem;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Built-in roles cannot be deleted or renamed by code.</summary>
    public bool IsSystem { get; private set; }
    public IReadOnlyList<RolePermission> Permissions => _permissions;

    public void SetPermissions(IEnumerable<string> codes)
    {
        var wanted = codes.Distinct().ToHashSet();
        _permissions.RemoveAll(p => !wanted.Contains(p.Permission));
        foreach (var code in wanted.Where(c => _permissions.All(p => p.Permission != c)))
            _permissions.Add(new RolePermission(Id, code));
    }
}

public sealed class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = "";
}

/// <summary>Role granted to an admin; OrgUnitId null means the whole organisation.</summary>
public sealed class AdminUserRole
{
    private AdminUserRole() { }

    public AdminUserRole(Guid adminUserId, Guid roleId, Guid? orgUnitId)
    {
        Id = Guid.NewGuid();
        AdminUserId = adminUserId;
        RoleId = roleId;
        OrgUnitId = orgUnitId;
    }

    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? OrgUnitId { get; private set; }
}

public sealed class OrgUnit
{
    private OrgUnit() { }

    public OrgUnit(string code, string name, Guid? parentId)
    {
        Id = Guid.NewGuid();
        Code = code.Trim();
        Name = name.Trim();
        ParentId = parentId;
    }

    public Guid Id { get; private set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
}

public sealed class CandidateGroup
{
    private CandidateGroup() { }

    public CandidateGroup(string name)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
    }

    public Guid Id { get; private set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid? OrgUnitId { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class CandidateGroupMember
{
    private CandidateGroupMember() { }

    public CandidateGroupMember(Guid groupId, Guid candidateId)
    {
        GroupId = groupId;
        CandidateId = candidateId;
    }

    public Guid GroupId { get; private set; }
    public Guid CandidateId { get; private set; }
}
