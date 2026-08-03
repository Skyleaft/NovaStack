using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;
using System.Security.Claims;

namespace Identity.Application.Features.Auth.Me;

// ── Query / Response ─────────────────────────────────────────────────────────
public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<CurrentUserResponse>;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsEmailVerified,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt);

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class GetCurrentUserQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(query.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);

        return new CurrentUserResponse(
            user.Id.Value,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsEmailVerified,
            roles.Select(r => r.Name).ToList().AsReadOnly(),
            user.CreatedAt);
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class GetCurrentUserEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/me", HandleAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Get the currently authenticated user's profile")
            .WithTags("Auth")
            .RequireAuthorization()
            .Produces<ApiResponse<CurrentUserResponse>>(StatusCodes.Status200OK)
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

        var result = await sender.Send(new GetCurrentUserQuery(userId), ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToHttpResult();
    }
}
