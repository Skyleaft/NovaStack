using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.SharedKernel.Results;
using System.Security.Claims;

namespace Identity.Application.Features.OIDC.UserInfo;

// ── Query / Response ─────────────────────────────────────────────────────────
public sealed record GetUserInfoQuery(Guid UserId) : IQuery<UserInfoResponse>;

/// <summary>
/// Standard OpenID Connect UserInfo claims.
/// Spec: https://openid.net/specs/openid-connect-core-1_0.html#UserInfoResponse
/// </summary>
public sealed record UserInfoResponse(
    string Sub,
    string Email,
    bool EmailVerified,
    string GivenName,
    string FamilyName,
    string Name,
    IReadOnlyList<string> Roles);

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class GetUserInfoQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository)
    : IQueryHandler<GetUserInfoQuery, UserInfoResponse>
{
    public async Task<Result<UserInfoResponse>> Handle(GetUserInfoQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(query.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);

        return new UserInfoResponse(
            Sub: user.Id.Value.ToString(),
            Email: user.Email,
            EmailVerified: user.IsEmailVerified,
            GivenName: user.FirstName,
            FamilyName: user.LastName,
            Name: user.FullName,
            Roles: roles.Select(r => r.Name).ToList().AsReadOnly());
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class UserInfoEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/connect/userinfo", HandleAsync)
            .WithName("OidcUserInfo")
            .WithSummary("OpenID Connect UserInfo endpoint — returns standard OIDC claims")
            .WithTags("OIDC")
            .RequireAuthorization()
            .Produces<UserInfoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        ISender sender,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var result = await sender.Send(new GetUserInfoQuery(userId), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}
