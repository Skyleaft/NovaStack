using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NovaStack.Infrastructure.Authentication;

/// <summary>
/// Concrete implementation of <see cref="IJwtTokenService"/> using HMAC-SHA256 symmetric signing.
/// Registered as <b>Scoped</b> by <c>AddNovaStackAuth</c>.
/// </summary>
internal sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _opts = jwtOptions.Value;

    /// <inheritdoc/>
    public string GenerateAccessToken(
        Guid userId,
        string email,
        IEnumerable<string> roles,
        IEnumerable<Claim>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            string.IsNullOrWhiteSpace(_opts.SecretKey)
                ? "default_dev_key_please_change_me_32chars!"
                : _opts.SecretKey));

        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        // RBAC — one claim per role (standard ASP.NET Core role claim)
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // OIDC scope claim
        claims.Add(new Claim("scope", _opts.OpenId.SupportedScopes));

        if (extraClaims is not null)
            claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <inheritdoc/>
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            string.IsNullOrWhiteSpace(_opts.SecretKey)
                ? "default_dev_key_please_change_me_32chars!"
                : _opts.SecretKey));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = false,      // Skip — we only care about the signature
            ValidateAudience = false,
            ValidateLifetime = false,    // Allow expired tokens for refresh flow
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
