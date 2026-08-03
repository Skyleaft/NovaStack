using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<IReadOnlyList<User>> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = context.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.Contains(s) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(s));
        }
        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? search, CancellationToken ct = default)
    {
        var query = context.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.Contains(s) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(s));
        }
        return await query.CountAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await context.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        context.Users.Update(user);
        return Task.CompletedTask;
    }
}
