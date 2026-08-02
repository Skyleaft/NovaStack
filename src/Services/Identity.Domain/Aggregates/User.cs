using Identity.Domain.DomainEvents;
using Identity.Domain.ValueObjects;
using NovaStack.SharedKernel.Common;
using NovaStack.SharedKernel.Exceptions;

namespace Identity.Domain.Aggregates;

/// <summary>
/// User aggregate root. Encapsulates identity invariants: credential management,
/// profile updates, activation state, and role assignments.
/// </summary>
public sealed class User : Entity<UserId>
{
    // ── State ───────────────────────────────────────────────────────────────
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Role> _roles = [];
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    // ── EF Core constructor ─────────────────────────────────────────────────
    private User() : base() { }

    // ── Factory ─────────────────────────────────────────────────────────────
    public static User Create(
        UserId id,
        string email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        Guard.NotNullOrWhiteSpace(email, nameof(email));
        Guard.NotNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        Guard.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.NotNullOrWhiteSpace(lastName, nameof(lastName));

        var user = new User
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            IsActive = true,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        user.RaiseDomainEvent(new UserCreatedDomainEvent(
            id.Value, email, firstName, lastName, DateTime.UtcNow));

        return user;
    }

    /// <summary>
    /// Reconstitutes a User from persisted state without raising domain events.
    /// Used by MongoDB repositories.
    /// </summary>
    public static User Reconstitute(
        UserId id,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        bool isActive,
        bool isEmailVerified,
        DateTime createdAt,
        DateTime? updatedAt) => new()
    {
        Id = id,
        Email = email,
        PasswordHash = passwordHash,
        FirstName = firstName,
        LastName = lastName,
        IsActive = isActive,
        IsEmailVerified = isEmailVerified,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt
    };

    // ── Behaviour ───────────────────────────────────────────────────────────
    public void SetPassword(string newPasswordHash)
    {
        Guard.NotNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        Guard.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.NotNullOrWhiteSpace(lastName, nameof(lastName));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void VerifyEmail()
    {
        IsEmailVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("User is already inactive.");
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new UserDeactivatedDomainEvent(Id.Value, Email));
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("User is already active.");
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public string FullName => $"{FirstName} {LastName}";
}
