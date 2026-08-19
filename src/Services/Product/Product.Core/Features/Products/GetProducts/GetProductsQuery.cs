using NovaStack.Contracts.Responses;
using Product.Core.Common.Abstractions;
using Product.Core.Features.Products.GetProductById;

namespace Product.Core.Features.Products.GetProducts;

/// <summary>Query to retrieve a paginated list of products.</summary>
public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string? SortBy = null,
    bool Descending = false
) : IQuery<PagedResponse<ProductResponse>>;
