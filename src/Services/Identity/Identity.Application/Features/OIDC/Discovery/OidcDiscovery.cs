using Identity.Application.Common.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NovaStack.Infrastructure.Authentication;

namespace Identity.Application.Features.OIDC.Discovery;

/// <summary>
/// Serves the OpenID Connect Discovery document at the standard well-known URL.
/// Spec: https://openid.net/specs/openid-connect-discovery-1_0.html
/// </summary>
public sealed class DiscoveryEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/openid-configuration", HandleAsync)
            .WithName("OidcDiscovery")
            .WithSummary("OpenID Connect Discovery document")
            .WithTags("OIDC")
            .AllowAnonymous()
            .Produces<OidcDiscoveryDocument>(StatusCodes.Status200OK);
    }

    private static IResult HandleAsync(IOptions<AuthenticationOptions> opts, HttpContext ctx)
    {
        var issuer = string.IsNullOrWhiteSpace(opts.Value.Issuer)
            ? $"{ctx.Request.Scheme}://{ctx.Request.Host}"
            : opts.Value.Issuer.TrimEnd('/');

        var isRsa = string.Equals(opts.Value.Signing.Algorithm, "RS256", StringComparison.OrdinalIgnoreCase);

        var doc = new OidcDiscoveryDocument(
            Issuer: issuer,
            AuthorizationEndpoint: $"{issuer}/connect/authorize",
            TokenEndpoint: $"{issuer}/api/v1/auth/login",
            UserinfoEndpoint: $"{issuer}/connect/userinfo",
            JwksUri: $"{issuer}/.well-known/jwks.json",
            RevocationEndpoint: $"{issuer}/api/v1/auth/revoke",
            ResponseTypesSupported: ["code", "token", "id_token"],
            GrantTypesSupported: ["authorization_code", "password", "refresh_token"],
            ScopesSupported: ["openid", "profile", "email"],
            TokenEndpointAuthMethodsSupported: ["client_secret_post", "client_secret_basic"],
            SubjectTypesSupported: ["public"],
            IdTokenSigningAlgValuesSupported: isRsa ? ["RS256"] : ["HS256"],
            ClaimsSupported: ["sub", "email", "given_name", "family_name", "roles", "iat", "exp", "jti"]
        );

        return Results.Ok(doc);
    }
}

// ── Discovery Document DTO ────────────────────────────────────────────────────
public sealed record OidcDiscoveryDocument(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string UserinfoEndpoint,
    string JwksUri,
    string RevocationEndpoint,
    IReadOnlyList<string> ResponseTypesSupported,
    IReadOnlyList<string> GrantTypesSupported,
    IReadOnlyList<string> ScopesSupported,
    IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
    IReadOnlyList<string> SubjectTypesSupported,
    IReadOnlyList<string> IdTokenSigningAlgValuesSupported,
    IReadOnlyList<string> ClaimsSupported);
