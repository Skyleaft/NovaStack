using Identity.Domain.ValueObjects;

namespace Identity.Infrastructure.Persistence;

/// <summary>Join entity for User <-> Role many-to-many relationship.</summary>
public sealed class UserRole
{
    public UserId UserId { get; set; } = null!;
    public RoleId RoleId { get; set; } = null!;
}
