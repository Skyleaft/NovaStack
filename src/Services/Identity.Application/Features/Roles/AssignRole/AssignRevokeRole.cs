using FluentValidation;
using Identity.Application.Common.Abstractions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Roles.AssignRole;

// ── Assign Command ────────────────────────────────────────────────────────────
public sealed record AssignRoleToUserCommand(Guid UserId, Guid RoleId) : ICommand;
public sealed record AssignRoleRequest(Guid RoleId);

public sealed class AssignRoleToUserCommandValidator : AbstractValidator<AssignRoleToUserCommand>
{
    public AssignRoleToUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

internal sealed class AssignRoleToUserCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AssignRoleToUserCommand>
{
    public async Task<Result> Handle(AssignRoleToUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", $"User '{command.UserId}' was not found.");

        var role = await roleRepository.GetByIdAsync(RoleId.From(command.RoleId), ct);
        if (role is null)
            return Error.NotFound("Role.NotFound", $"Role '{command.RoleId}' was not found.");

        await roleRepository.AssignToUserAsync(user.Id, role.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class AssignRoleEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/users/{userId:guid}/roles", HandleAsync)
            .WithName("AssignRoleToUser")
            .WithSummary("Assign a role to a user (Admin only)")
            .WithTags("Roles")
            .RequireAuthorization("Admin")
            .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        Guid userId,
        AssignRoleRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new AssignRoleToUserCommand(userId, request.RoleId), ct);
        return result.IsSuccess
            ? Results.Ok(ApiResponse.Ok<object?>(null, "Role assigned successfully."))
            : result.Error.ToHttpResult();
    }
}

// ── Revoke Command ────────────────────────────────────────────────────────────
public sealed record RevokeRoleFromUserCommand(Guid UserId, Guid RoleId) : ICommand;

internal sealed class RevokeRoleFromUserCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeRoleFromUserCommand>
{
    public async Task<Result> Handle(RevokeRoleFromUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct);
        if (user is null)
            return Error.NotFound("User.NotFound", $"User '{command.UserId}' was not found.");

        var role = await roleRepository.GetByIdAsync(RoleId.From(command.RoleId), ct);
        if (role is null)
            return Error.NotFound("Role.NotFound", $"Role '{command.RoleId}' was not found.");

        await roleRepository.RevokeFromUserAsync(user.Id, role.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class RevokeRoleEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/v1/users/{userId:guid}/roles/{roleId:guid}", HandleAsync)
            .WithName("RevokeRoleFromUser")
            .WithSummary("Remove a role from a user (Admin only)")
            .WithTags("Roles")
            .RequireAuthorization("Admin")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        Guid userId,
        Guid roleId,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new RevokeRoleFromUserCommand(userId, roleId), ct);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToHttpResult();
    }
}
