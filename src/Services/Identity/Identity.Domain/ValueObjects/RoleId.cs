using NovaStack.SharedKernel.ValueObjects;

namespace Identity.Domain.ValueObjects;

/// <summary>Strongly-typed Role identifier.</summary>
public sealed class RoleId : ValueObject
{
    public Guid Value { get; }

    private RoleId(Guid value) => Value = value;

    public static RoleId New() => new(Guid.NewGuid());

    public static RoleId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RoleId cannot be an empty GUID.", nameof(value));
        return new RoleId(value);
    }

    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(RoleId id) => id.Value;
    public static implicit operator RoleId(Guid id) => From(id);
}
