using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Repositories;

/// <summary>Repository contract for the Role aggregate.</summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task AssignToUserAsync(UserId userId, RoleId roleId, CancellationToken ct = default);
    Task RevokeFromUserAsync(UserId userId, RoleId roleId, CancellationToken ct = default);
}
