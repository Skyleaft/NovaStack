using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository(IdentityDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        await context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(UserId userId, CancellationToken ct = default) =>
        await context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default) =>
        await context.RefreshTokens.AddAsync(refreshToken, ct);

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        context.RefreshTokens.Update(refreshToken);
        return Task.CompletedTask;
    }

    public async Task RevokeAllByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
            token.Revoke();
    }
}
