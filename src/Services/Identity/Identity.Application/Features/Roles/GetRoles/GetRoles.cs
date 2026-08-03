using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Roles.GetRoles;

// ── Query / Response ─────────────────────────────────────────────────────────
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleResponse>>;

public sealed record RoleResponse(Guid Id, string Name, string Description, bool IsSystemRole);

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class GetRolesQueryHandler(IRoleRepository roleRepository)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(GetRolesQuery query, CancellationToken ct)
    {
        var roles = await roleRepository.GetAllAsync(ct);
        return roles
            .Select(r => new RoleResponse(r.Id.Value, r.Name, r.Description, r.IsSystemRole))
            .ToList()
            .AsReadOnly();
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class GetRolesEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/roles", HandleAsync)
            .WithName("GetRoles")
            .WithSummary("List all RBAC roles (Admin only)")
            .WithTags("Roles")
            .RequireAuthorization("Admin")
            .Produces<ApiResponse<IReadOnlyList<RoleResponse>>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> HandleAsync(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetRolesQuery(), ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToHttpResult();
    }
}
