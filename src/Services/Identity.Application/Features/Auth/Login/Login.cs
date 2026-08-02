using FluentValidation;
using Identity.Application.Common.Abstractions;
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
using DomainRefreshToken = Identity.Domain.Aggregates.RefreshToken;

namespace Identity.Application.Features.Auth.Login;

// ── Request / Response DTOs ───────────────────────────────────────────────────
public sealed record LoginCommand(string Email, string Password) : ICommand<TokenResponse>;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    IReadOnlyList<string> Roles);

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions)
    : ICommandHandler<LoginCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, ct);
        if (user is null || !user.IsActive)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        var verifyResult = passwordHasher.VerifyHashedPassword(null!, user.PasswordHash, command.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        // Fetch roles for RBAC claims
        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);
        var roleNames = roles.Select(r => r.Name).ToList();

        // Issue tokens
        var accessToken = jwtTokenService.GenerateAccessToken(user.Id.Value, user.Email, roleNames);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        var refreshToken = DomainRefreshToken.Create(
            RefreshTokenId.New(),
            rawRefreshToken,
            user.Id,
            jwtOptions.Value.RefreshTokenExpiryDays);

        await refreshTokenRepository.AddAsync(refreshToken, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new TokenResponse(
            accessToken,
            rawRefreshToken,
            "Bearer",
            jwtOptions.Value.ExpiryMinutes * 60,
            roleNames.AsReadOnly());
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class LoginEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Authenticate and receive access + refresh tokens")
            .WithTags("Auth")
            .AllowAnonymous()
            .Produces<ApiResponse<TokenResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        LoginCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(result.Value, "Login successful."))
            : result.Error.ToHttpResult();
    }
}
