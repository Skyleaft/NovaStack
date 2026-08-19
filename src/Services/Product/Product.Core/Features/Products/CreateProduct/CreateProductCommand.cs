using Product.Core.Common.Abstractions;

namespace Product.Core.Features.Products.CreateProduct;

/// <summary>Command to create a new product.</summary>
public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity
) : ICommand<Guid>;
