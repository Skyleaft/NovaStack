using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;
using System.Security.Claims;

namespace Identity.Application.Features.Auth.Logout;

// ── Command ───────────────────────────────────────────────────────────────────
/// <summary>Revokes ALL active refresh tokens for the currently authenticated user.</summary>
public sealed record LogoutCommand(Guid UserId) : ICommand;

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken ct)
    {
        await refreshTokenRepository.RevokeAllByUserIdAsync(UserId.From(command.UserId), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class LogoutEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/logout", HandleAsync)
            .WithName("Logout")
            .WithSummary("Revoke all active refresh tokens (logout from all devices)")
            .WithTags("Auth")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
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

        var result = await sender.Send(new LogoutCommand(userId), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}
