using NovaStack.SharedKernel.Abstractions;
using NovaStack.SharedKernel.Results;
using Product.Application.Common.Abstractions;
using Product.Domain.Repositories;
using Product.Domain.ValueObjects;
using DomainProduct = Product.Domain.Aggregates.Product;

namespace Product.Application.Features.Products.CreateProduct;

/// <summary>Handles the <see cref="CreateProductCommand"/>.</summary>
internal sealed class CreateProductCommandHandler(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateProductCommand command,
        CancellationToken ct)
    {
        // Check for duplicate name
        if (await productRepository.ExistsByNameAsync(command.Name, ct))
            return Error.Conflict("Product.NameConflict",
                $"A product with the name '{command.Name}' already exists.");

        var product = DomainProduct.Create(
            ProductId.New(),
            command.Name,
            command.Description,
            Money.Create(command.Price, command.Currency),
            command.StockQuantity);

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return product.Id.Value;
    }
}
