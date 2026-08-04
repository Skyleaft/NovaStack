using NovaStack.SharedKernel.ValueObjects;

namespace Identity.Domain.ValueObjects;

/// <summary>Strongly-typed User identifier.</summary>
public sealed class UserId : ValueObject
{
    public Guid Value { get; }

    private UserId(Guid value) => Value = value;

    public static UserId New() => new(Guid.CreateVersion7());

    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be an empty GUID.", nameof(value));
        return new UserId(value);
    }

    public static UserId From(string value) =>
        Guid.TryParse(value, out var id)
            ? From(id)
            : throw new ArgumentException($"'{value}' is not a valid UserId.", nameof(value));

    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(UserId id) => id.Value;
    public static implicit operator UserId(Guid id) => From(id);
}
