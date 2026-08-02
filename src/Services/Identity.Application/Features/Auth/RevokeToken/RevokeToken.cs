using FluentValidation;
using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Auth.RevokeToken;

// ── Command ───────────────────────────────────────────────────────────────────
public sealed record RevokeTokenCommand(string RefreshToken) : ICommand;

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class RevokeTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeTokenCommand>
{
    public async Task<Result> Handle(RevokeTokenCommand command, CancellationToken ct)
    {
        var token = await refreshTokenRepository.GetByTokenAsync(command.RefreshToken, ct);
        if (token is null)
            return Error.NotFound("Auth.TokenNotFound", "Refresh token not found.");

        if (token.IsRevoked)
            return Error.Conflict("Auth.TokenAlreadyRevoked", "Refresh token is already revoked.");

        token.Revoke();
        await refreshTokenRepository.UpdateAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class RevokeTokenEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/revoke", HandleAsync)
            .WithName("RevokeToken")
            .WithSummary("Revoke a specific refresh token")
            .WithTags("Auth")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        RevokeTokenCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}
