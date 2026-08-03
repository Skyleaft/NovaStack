using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Repositories;

/// <summary>Repository contract for the RefreshToken aggregate.</summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(UserId userId, CancellationToken ct = default);
}
