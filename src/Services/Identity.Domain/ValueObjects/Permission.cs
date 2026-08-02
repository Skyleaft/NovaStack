using NovaStack.SharedKernel.Exceptions;
using NovaStack.SharedKernel.ValueObjects;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// Represents an RBAC permission as a resource + action pair.
/// Example: Resource="products", Action="read" → "products:read".
/// </summary>
public sealed class Permission : ValueObject
{
    public string Resource { get; }
    public string Action { get; }

    private Permission(string resource, string action)
    {
        Resource = resource;
        Action = action;
    }

    public static Permission Create(string resource, string action)
    {
        if (string.IsNullOrWhiteSpace(resource))
            throw new DomainException("Permission resource cannot be empty.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Permission action cannot be empty.");

        return new Permission(
            resource.Trim().ToLowerInvariant(),
            action.Trim().ToLowerInvariant());
    }

    public static Permission From(string value)
    {
        var parts = value.Split(':', 2);
        if (parts.Length != 2)
            throw new DomainException($"Invalid permission format: '{value}'. Expected 'resource:action'.");
        return Create(parts[0], parts[1]);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Resource;
        yield return Action;
    }

    public override string ToString() => $"{Resource}:{Action}";
}
