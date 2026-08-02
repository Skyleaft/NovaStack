using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

internal sealed class RoleRepository(IdentityDbContext context) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default) =>
        await context.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await context.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default) =>
        await context.Roles.AnyAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) =>
        await context.Roles.OrderBy(r => r.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Role>> GetByUserIdAsync(UserId userId, CancellationToken ct = default) =>
        await context.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Roles)
            .ToListAsync(ct);

    public async Task AddAsync(Role role, CancellationToken ct = default) =>
        await context.Roles.AddAsync(role, ct);

    public async Task AssignToUserAsync(UserId userId, RoleId roleId, CancellationToken ct = default)
    {
        var user = await context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"User {userId.Value} not found.");

        var role = await context.Roles.FindAsync([roleId], ct)
            ?? throw new InvalidOperationException($"Role {roleId.Value} not found.");

        // EF Core many-to-many: just add to the collection — it handles the join row
        ((List<Role>)user.Roles).Add(role);
    }

    public async Task RevokeFromUserAsync(UserId userId, RoleId roleId, CancellationToken ct = default)
    {
        var user = await context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException($"User {userId.Value} not found.");

        var role = user.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role is not null)
            ((List<Role>)user.Roles).Remove(role);
    }
}
