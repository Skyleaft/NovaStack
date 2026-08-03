using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Users.DeactivateUser;

// ── Command ───────────────────────────────────────────────────────────────────
public sealed record DeactivateUserCommand(Guid UserId) : ICommand;

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", $"User '{command.UserId}' was not found.");

        user.Deactivate();

        // Revoke all tokens on deactivation — deny further logins
        await refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class DeactivateUserEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/users/{id:guid}", HandleAsync)
            .WithName("DeactivateUser")
            .WithSummary("Soft-deactivate a user and revoke all their tokens (Admin only)")
            .WithTags("Users")
            .RequireAuthorization("Admin")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}
