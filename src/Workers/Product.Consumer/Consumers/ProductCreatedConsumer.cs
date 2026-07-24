using MassTransit;
using Microsoft.Extensions.Logging;
using NovaStack.Contracts.IntegrationEvents;

namespace Product.Consumer.Consumers;

/// <summary>
/// MassTransit consumer for <see cref="ProductCreatedIntegrationEvent"/>.
/// Add your downstream business logic here (e.g., sync to search index, send email, update cache).
/// </summary>
public sealed class ProductCreatedConsumer(
    ILogger<ProductCreatedConsumer> logger)
    : IConsumer<ProductCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProductCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "[ProductCreatedConsumer] Received ProductCreatedIntegrationEvent. " +
            "ProductId={ProductId}, Name={Name}, Price={Price} {Currency}",
            message.ProductId,
            message.Name,
            message.Price,
            message.Currency);

        // TODO: Add downstream logic here, e.g.:
        // - Sync to Elasticsearch / search index
        // - Update a read model / projection
        // - Trigger a notification
        // - Call an external API

        await Task.CompletedTask;
    }
}
