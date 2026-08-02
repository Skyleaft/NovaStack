using FluentValidation;
using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Users.UpdateUser;

// ── Command ───────────────────────────────────────────────────────────────────
public sealed record UpdateUserCommand(Guid UserId, string FirstName, string LastName) : ICommand;

public sealed record UpdateUserRequest(string FirstName, string LastName);

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", $"User '{command.UserId}' was not found.");

        user.UpdateProfile(command.FirstName, command.LastName);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class UpdateUserEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/users/{id:guid}", HandleAsync)
            .WithName("UpdateUser")
            .WithSummary("Update a user's profile (Admin only)")
            .WithTags("Users")
            .RequireAuthorization("Admin")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        UpdateUserRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new UpdateUserCommand(id, request.FirstName, request.LastName), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}
