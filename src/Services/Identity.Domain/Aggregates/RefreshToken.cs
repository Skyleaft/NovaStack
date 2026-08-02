using Identity.Domain.ValueObjects;
using NovaStack.SharedKernel.Common;
using NovaStack.SharedKernel.Exceptions;

namespace Identity.Domain.Aggregates;

/// <summary>
/// Represents an opaque refresh token issued to a user.
/// Stored in the database to enable token rotation and revocation.
/// </summary>
public sealed class RefreshToken : Entity<RefreshTokenId>
{
    // ── State ───────────────────────────────────────────────────────────────
    public string Token { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsRevoked { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    // ── EF Core constructor ─────────────────────────────────────────────────
    private RefreshToken() : base() { }

    // ── Factory ─────────────────────────────────────────────────────────────
    public static RefreshToken Create(
        RefreshTokenId id,
        string token,
        UserId userId,
        int expiryDays)
    {
        Guard.NotNullOrWhiteSpace(token, nameof(token));
        Guard.NotNull(userId, nameof(userId));

        if (expiryDays <= 0)
            throw new DomainException("Refresh token expiry must be a positive number of days.");

        return new RefreshToken
        {
            Id = id,
            Token = token,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    public static RefreshToken Reconstitute(
        RefreshTokenId id,
        string token,
        UserId userId,
        DateTime expiresAt,
        DateTime createdAt,
        DateTime? revokedAt,
        bool isRevoked) => new()
    {
        Id = id,
        Token = token,
        UserId = userId,
        ExpiresAt = expiresAt,
        CreatedAt = createdAt,
        RevokedAt = revokedAt,
        IsRevoked = isRevoked
    };

    // ── Behaviour ───────────────────────────────────────────────────────────
    public void Revoke()
    {
        if (IsRevoked) throw new DomainException("Refresh token is already revoked.");
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
}
