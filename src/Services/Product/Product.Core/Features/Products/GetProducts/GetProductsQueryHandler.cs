using MapsterMapper;
using NovaStack.Contracts.Responses;
using NovaStack.SharedKernel.Results;
using Product.Core.Common.Abstractions;
using Product.Core.Features.Products.GetProductById;
using Product.Core.Domain.Repositories;

namespace Product.Core.Features.Products.GetProducts;

/// <summary>Handles the <see cref="GetProductsQuery" />.</summary>
internal sealed class GetProductsQueryHandler(IProductRepository productRepository, IMapper mapper)
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

        var mappedItems = mapper.Map<IEnumerable<ProductResponse>>(pagedProducts.Items);

        var response = PagedResponse<ProductResponse>.Create(
            mappedItems,
            pagedProducts.Page,
            pagedProducts.PageSize,
            pagedProducts.TotalCount);

        return response;
    }
}
