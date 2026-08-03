using FluentValidation;
using Identity.Application.Common.Abstractions;
using Identity.Application.Features.Auth.Login;
using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NovaStack.Contracts.Responses;
using NovaStack.Infrastructure.Authentication;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;
using System.Security.Claims;
using DomainRefreshToken = Identity.Domain.Aggregates.RefreshToken;

namespace Identity.Application.Features.Auth.RefreshToken;

// ── Command ───────────────────────────────────────────────────────────────────
public sealed record RefreshTokenCommand(
    string AccessToken,
    string RefreshToken) : ICommand<TokenResponse>;

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService,
    IOptions<AuthenticationOptions> jwtOptions)
    : ICommandHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        // Validate the expired access token to extract claims
        var principal = jwtTokenService.GetPrincipalFromExpiredToken(command.AccessToken);
        if (principal is null)
            return Error.Unauthorized("Auth.InvalidToken", "The access token is invalid.");

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Error.Unauthorized("Auth.InvalidToken", "The access token contains no valid user identity.");

        // Verify the refresh token in the DB
        var storedToken = await refreshTokenRepository.GetByTokenAsync(command.RefreshToken, ct);
        if (storedToken is null || !storedToken.IsActive || storedToken.UserId.Value != userId)
            return Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");

        var user = await userRepository.GetByIdAsync(UserId.From(userId), ct);
        if (user is null || !user.IsActive)
            return Error.Unauthorized("Auth.UserInactive", "The user account is inactive.");

        // Rotate: revoke old, issue new
        storedToken.Revoke();
        await refreshTokenRepository.UpdateAsync(storedToken, ct);

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);
        var roleNames = roles.Select(r => r.Name).ToList();

        var newAccessToken = jwtTokenService.GenerateAccessToken(user.Id.Value, user.Email, roleNames);
        var newRawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var newRefreshToken = DomainRefreshToken.Create(
            RefreshTokenId.New(),
            newRawRefreshToken,
            user.Id,
            jwtOptions.Value.RefreshToken.LifetimeDays);

        await refreshTokenRepository.AddAsync(newRefreshToken, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new TokenResponse(
            newAccessToken,
            newRawRefreshToken,
            "Bearer",
            jwtOptions.Value.AccessToken.LifetimeMinutes * 60,
            roleNames.AsReadOnly());
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class RefreshTokenEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithSummary("Rotate an expired access token using a valid refresh token")
            .WithTags("Auth")
            .AllowAnonymous()
            .Produces<ApiResponse<TokenResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        RefreshTokenCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value, "Token refreshed successfully."))
            : result.Error.ToHttpResult();
    }
}
