using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Identity.Application.Common.Abstractions;
using NovaStack.Infrastructure.Authentication;

namespace Identity.Application.Features.OIDC.Discovery;

/// <summary>
/// Serves the JSON Web Key Set (JWKS) document at the standard well-known URL.
/// Spec: https://tools.ietf.org/html/rfc7517
/// </summary>
public sealed class JwksEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/jwks.json", HandleAsync)
            .WithName("OidcJwks")
            .WithSummary("JSON Web Key Set (JWKS) public keys")
            .WithTags("OIDC")
            .AllowAnonymous()
            .Produces<JwksDocument>(StatusCodes.Status200OK);
    }

    private static IResult HandleAsync(IJwtTokenService jwtTokenService)
    {
        try
        {
            var jwk = jwtTokenService.GetPublicKeyDto();
            return Results.Ok(new JwksDocument(new[] { jwk }));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "JWKS Endpoint Unavailable");
        }
    }
}
