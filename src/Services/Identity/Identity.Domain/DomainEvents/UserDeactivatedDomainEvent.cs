using NovaStack.SharedKernel.Abstractions;

namespace Identity.Domain.DomainEvents;

/// <summary>Raised when a User aggregate is deactivated (soft-delete).</summary>
public sealed record UserDeactivatedDomainEvent(
    Guid UserId,
    string Email) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
