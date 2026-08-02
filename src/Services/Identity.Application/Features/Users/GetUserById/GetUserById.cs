using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Users.GetUserById;

// ── Query / Response ─────────────────────────────────────────────────────────
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDetailResponse>;

public sealed record UserDetailResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsActive,
    bool IsEmailVerified,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class GetUserByIdQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository)
    : IQueryHandler<GetUserByIdQuery, UserDetailResponse>
{
    public async Task<Result<UserDetailResponse>> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(query.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", $"User '{query.UserId}' was not found.");

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);

        return new UserDetailResponse(
            user.Id.Value,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.IsActive,
            user.IsEmailVerified,
            roles.Select(r => r.Name).ToList().AsReadOnly(),
            user.CreatedAt,
            user.UpdatedAt);
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class GetUserByIdEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users/{id:guid}", HandleAsync)
            .WithName("GetUserById")
            .WithSummary("Get a user by ID (Admin only)")
            .WithTags("Users")
            .RequireAuthorization("Admin")
            .Produces<ApiResponse<UserDetailResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToHttpResult();
    }
}
