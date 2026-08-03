using Dapper;
using Identity.Application.Common.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Common;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Users.GetUsers;

// ── Query / Response ─────────────────────────────────────────────────────────
public sealed record GetUsersQuery(int Page, int PageSize, string? Search) : IQuery<PagedResponse<UserSummaryResponse>>;

public sealed class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}


// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class GetUsersQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserSummaryResponse>>
{
    public async Task<Result<PagedResponse<UserSummaryResponse>>> Handle(GetUsersQuery query, CancellationToken ct)
    {
        using var connection = sqlConnectionFactory.CreateConnection();

        var searchFilter = string.IsNullOrWhiteSpace(query.Search)
            ? string.Empty
            : $"%{query.Search.Trim().ToLower()}%";

        const string countSql = """
            SELECT COUNT(*) FROM identity.users
            WHERE (@Search = '' OR LOWER(email) LIKE @Search
                OR LOWER(first_name || ' ' || last_name) LIKE @Search)
            """;

        const string dataSql = """
            SELECT id, email,
                   first_name || ' ' || last_name AS full_name,
                   is_active, is_email_verified, created_at
            FROM identity.users
            WHERE (@Search = '' OR LOWER(email) LIKE @Search
                OR LOWER(first_name || ' ' || last_name) LIKE @Search)
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var p = new { Search = searchFilter, PageSize = query.PageSize, Offset = (query.Page - 1) * query.PageSize };
        var total = await connection.ExecuteScalarAsync<int>(countSql, p);
        var items = (await connection.QueryAsync<UserSummaryResponse>(dataSql, p)).ToList();

        return PagedResponse<UserSummaryResponse>.Create(items, query.Page, query.PageSize, total);
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class GetUsersEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", HandleAsync)
            .WithName("GetUsers")
            .WithSummary("Paginated list of all users (Admin only)")
            .WithTags("Users")
            .RequireAuthorization("Admin")
            .Produces<ApiResponse<PagedResponse<UserSummaryResponse>>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleAsync(
        int page,
        int pageSize,
        string? search,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetUsersQuery(
            page <= 0 ? 1 : page,
            pageSize is <= 0 or > 100 ? 20 : pageSize,
            search), ct);

        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToHttpResult();
    }
}
