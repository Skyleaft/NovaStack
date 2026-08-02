namespace NovaStack.Infrastructure.Authentication;

/// <summary>JWT bearer authentication settings.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>OpenID Connect discovery metadata settings.</summary>
    public OpenIdOptions OpenId { get; set; } = new();
}

/// <summary>OpenID Connect configuration exposed on the discovery endpoint.</summary>
public sealed class OpenIdOptions
{
    /// <summary>The authority base URL (e.g. https://identity.example.com).</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Scopes advertised in discovery (space-separated, e.g. "openid profile email").</summary>
    public string SupportedScopes { get; set; } = "openid profile email";

    /// <summary>Response types supported (default: code, token, id_token).</summary>
    public string SupportedResponseTypes { get; set; } = "code token id_token";

    /// <summary>Grant types supported.</summary>
    public string SupportedGrantTypes { get; set; } = "authorization_code password refresh_token";
}
