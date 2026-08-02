using Identity.Domain.ValueObjects;
using NovaStack.SharedKernel.Common;
using NovaStack.SharedKernel.Exceptions;

namespace Identity.Domain.Aggregates;

/// <summary>
/// Role aggregate root. Represents an RBAC role with a named set of permissions.
/// Examples: "Admin", "ProductManager", "Viewer".
/// </summary>
public sealed class Role : Entity<RoleId>
{
    // ── State ───────────────────────────────────────────────────────────────
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool IsSystemRole { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<Permission> _permissions = [];
    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    // ── EF Core constructor ─────────────────────────────────────────────────
    private Role() : base() { }

    // ── Factory ─────────────────────────────────────────────────────────────
    public static Role Create(RoleId id, string name, string description, bool isSystemRole = false)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));

        return new Role
        {
            Id = id,
            Name = name.Trim(),
            Description = description.Trim(),
            IsSystemRole = isSystemRole,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Role Reconstitute(
        RoleId id,
        string name,
        string description,
        bool isSystemRole,
        DateTime createdAt,
        IEnumerable<Permission> permissions)
    {
        var role = new Role
        {
            Id = id,
            Name = name,
            Description = description,
            IsSystemRole = isSystemRole,
            CreatedAt = createdAt
        };
        role._permissions.AddRange(permissions);
        return role;
    }

    // ── Behaviour ───────────────────────────────────────────────────────────
    public void AssignPermission(Permission permission)
    {
        if (_permissions.Contains(permission))
            throw new DomainException($"Permission '{permission}' is already assigned to role '{Name}'.");
        _permissions.Add(permission);
    }

    public void RevokePermission(Permission permission)
    {
        if (!_permissions.Remove(permission))
            throw new DomainException($"Permission '{permission}' is not assigned to role '{Name}'.");
    }

    public bool HasPermission(string resource, string action) =>
        _permissions.Any(p =>
            p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) &&
            p.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
}
