using NovaStack.SharedKernel.Abstractions;

namespace Identity.Domain.DomainEvents;

/// <summary>Raised when a new User aggregate is successfully created.</summary>
public sealed record UserCreatedDomainEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTime OccurredAt) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
