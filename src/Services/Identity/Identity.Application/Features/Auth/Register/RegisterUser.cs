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
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Auth.Register;

// ── Command ──────────────────────────────────────────────────────────────────
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : ICommand<Guid>;

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken ct)
    {
        if (await userRepository.ExistsByEmailAsync(command.Email, ct))
            return Error.Conflict("User.EmailConflict", $"Email '{command.Email}' is already registered.");

        var id = UserId.New();
        var hash = passwordHasher.HashPassword(null!, command.Password);

        var user = User.Create(id, command.Email, hash, command.FirstName, command.LastName);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.Id.Value;
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class RegisterUserEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/register", HandleAsync)
            .WithName("RegisterUser")
            .WithSummary("Register a new user account")
            .WithTags("Auth")
            .AllowAnonymous()
            .Produces<ApiResponse<Guid>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAsync(
        RegisterUserCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/v1/users/{result.Value}",
                ApiResponse.Ok(result.Value, "User registered successfully."))
            : result.Error.ToHttpResult();
    }
}
