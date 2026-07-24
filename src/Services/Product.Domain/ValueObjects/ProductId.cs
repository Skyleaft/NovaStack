using NovaStack.SharedKernel.ValueObjects;

namespace Product.Domain.ValueObjects;

/// <summary>Strongly-typed Product identifier.</summary>
public sealed class ProductId : ValueObject
{
    public Guid Value { get; }

    private ProductId(Guid value) => Value = value;

    public static ProductId New() => new(Guid.NewGuid());

    public static ProductId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ProductId cannot be an empty GUID.", nameof(value));
        return new ProductId(value);
    }

    public static ProductId From(string value) =>
        Guid.TryParse(value, out var id)
            ? From(id)
            : throw new ArgumentException($"'{value}' is not a valid ProductId.", nameof(value));

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ProductId id) => id.Value;
    public static implicit operator ProductId(Guid id) => From(id);
}
