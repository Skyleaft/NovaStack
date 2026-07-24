using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;
using Product.Application.Common.Abstractions;
using Product.Application.Features.Products.GetProductById;
using Product.Domain.Repositories;

namespace Product.Application.Features.Products.GetProducts;

/// <summary>Handles the <see cref="GetProductsQuery"/>.</summary>
internal sealed class GetProductsQueryHandler(IProductRepository productRepository)
    : IQueryHandler<GetProductsQuery, PagedResponse<ProductResponse>>
{
    public async Task<Result<PagedResponse<ProductResponse>>> Handle(
        GetProductsQuery query,
        CancellationToken ct)
    {
        var pagedProducts = await productRepository.GetPagedAsync(
            query.Page,
            query.PageSize,
            query.Search,
            ct);

        var response = PagedResponse<ProductResponse>.Create(
            pagedProducts.Items.Select(p => new ProductResponse(
                p.Id.Value,
                p.Name,
                p.Description,
                p.Price.Amount,
                p.Price.Currency,
                p.StockQuantity,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt)),
            pagedProducts.Page,
            pagedProducts.PageSize,
            pagedProducts.TotalCount);

        return response;
    }
}
