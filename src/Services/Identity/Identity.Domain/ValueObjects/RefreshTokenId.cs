using NovaStack.SharedKernel.ValueObjects;

namespace Identity.Domain.ValueObjects;

/// <summary>Strongly-typed RefreshToken identifier.</summary>
public sealed class RefreshTokenId : ValueObject
{
    public Guid Value { get; }

    private RefreshTokenId(Guid value) => Value = value;

    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    public static RefreshTokenId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RefreshTokenId cannot be an empty GUID.", nameof(value));
        return new RefreshTokenId(value);
    }

    protected override IEnumerable<object?> GetAtomicValues() { yield return Value; }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(RefreshTokenId id) => id.Value;
    public static implicit operator RefreshTokenId(Guid id) => From(id);
}
