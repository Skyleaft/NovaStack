using System.Security.Claims;

namespace NovaStack.Infrastructure.Authentication;

/// <summary>
/// Contract for JWT access-token and refresh-token generation.
/// Implementations are registered in <c>InfrastructureServiceExtensions.AddNovaStackAuth</c>.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT access token containing the standard OpenID claims
    /// (<c>sub</c>, <c>email</c>, <c>roles</c>, <c>scope</c>, <c>jti</c>, <c>iat</c>).
    /// </summary>
    string GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<Claim>? extraClaims = null);

    /// <summary>Generates a cryptographically-random opaque refresh token string.</summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates an expired (or active) access token and returns the embedded claims principal.
    /// Returns <c>null</c> if the token is structurally invalid.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
