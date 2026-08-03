using FluentValidation;
using Identity.Application.Common.Abstractions;
using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;

namespace Identity.Application.Features.Roles.CreateRole;

// ── Command ───────────────────────────────────────────────────────────────────
public sealed record CreateRoleCommand(string Name, string Description) : ICommand<Guid>;

// ── Validator ─────────────────────────────────────────────────────────────────
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z][a-zA-Z0-9_-]*$")
            .WithMessage("Role name must start with a letter and contain only letters, digits, underscores, or hyphens.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
internal sealed class CreateRoleCommandHandler(
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken ct)
    {
        if (await roleRepository.ExistsByNameAsync(command.Name, ct))
            return Error.Conflict("Role.NameConflict", $"Role '{command.Name}' already exists.");

        var role = Role.Create(RoleId.New(), command.Name, command.Description);

        await roleRepository.AddAsync(role, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return role.Id.Value;
    }
}

// ── Endpoint ──────────────────────────────────────────────────────────────────
public sealed class CreateRoleEndpoint : IEndpointDefinition
{
    public void DefineEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/roles", HandleAsync)
            .WithName("CreateRole")
            .WithSummary("Create a new RBAC role (Admin only)")
            .WithTags("Roles")
            .RequireAuthorization("Admin")
            .Produces<ApiResponse<Guid>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleAsync(
        CreateRoleCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/v1/roles/{result.Value}",
                ApiResponse.Ok(result.Value, "Role created successfully."))
            : result.Error.ToHttpResult();
    }
}
