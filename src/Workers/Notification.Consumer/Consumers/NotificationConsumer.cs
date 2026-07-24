using MassTransit;
using Microsoft.Extensions.Logging;
using NovaStack.Contracts.IntegrationEvents;

namespace Notification.Consumer.Consumers;

/// <summary>
/// Receives integration events and dispatches notifications (email, push, SMS, webhook).
/// Extend this consumer to handle multiple notification event types.
/// </summary>
public sealed class NotificationConsumer(ILogger<NotificationConsumer> logger)
    : IConsumer<ProductCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProductCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "[NotificationConsumer] Product created notification. ProductId={ProductId}, Name={Name}",
            message.ProductId, message.Name);

        // TODO: Dispatch notifications via your preferred channel:
        // - Email (SendGrid, SMTP)
        // - Push notifications (FCM, APNs)
        // - SMS (Twilio)
        // - Webhook relay
        // - Slack / Teams bot

        await Task.CompletedTask;
    }
}
