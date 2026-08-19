using MongoDB.Driver;
using NovaStack.Infrastructure.Persistence.MongoDb;
using Product.Core.Persistence.Documents;

namespace Product.Core.Persistence;

/// <summary>
///     MongoDB context for the Product service. Exposes the <c>products</c> collection
///     and is registered as a scoped service when <c>Database:Provider</c> is <c>MongoDB</c>.
/// </summary>
public sealed class ProductMongoDbContext : MongoDbContextBase
{
    private const string CollectionName = "products";

    public ProductMongoDbContext(IMongoClient client, string databaseName)
        : base(client, databaseName)
    {
    }

    /// <summary>The products collection.</summary>
    public IMongoCollection<ProductDocument> Products =>
        GetCollection<ProductDocument>(CollectionName);
}
