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

    private static IResult HandleAsync(IOptions<JwtOptions> opts, HttpContext ctx)
    {
        var authority = string.IsNullOrWhiteSpace(opts.Value.OpenId.Authority)
            ? $"{ctx.Request.Scheme}://{ctx.Request.Host}"
            : opts.Value.OpenId.Authority;

        var doc = new OidcDiscoveryDocument(
            Issuer: opts.Value.Issuer,
            AuthorizationEndpoint: $"{authority}/connect/authorize",
            TokenEndpoint: $"{authority}/api/v1/auth/login",
            UserinfoEndpoint: $"{authority}/connect/userinfo",
            JwksUri: $"{authority}/.well-known/jwks.json",
            RevocationEndpoint: $"{authority}/api/v1/auth/revoke",
            ResponseTypesSupported: opts.Value.OpenId.SupportedResponseTypes.Split(' '),
            GrantTypesSupported: opts.Value.OpenId.SupportedGrantTypes.Split(' '),
            ScopesSupported: opts.Value.OpenId.SupportedScopes.Split(' '),
            TokenEndpointAuthMethodsSupported: ["client_secret_post", "client_secret_basic"],
            SubjectTypesSupported: ["public"],
            IdTokenSigningAlgValuesSupported: ["HS256"],
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
