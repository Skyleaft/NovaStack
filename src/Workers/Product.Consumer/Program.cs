using MassTransit;
using NovaStack.Contracts.IntegrationEvents;
using NovaStack.Infrastructure.Logging;
using NovaStack.Infrastructure.Messaging.Options;
using Product.Consumer.Consumers;
using Serilog;

LoggingExtensions.BootstrapLogger();

try
{
    Log.Information("Starting Product.Consumer...");

    var builder = Host.CreateApplicationBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Logging.ClearProviders();

    // ── MassTransit ──────────────────────────────────────────────────────────
    var messagingOptions = builder.Configuration
        .GetSection(MessagingOptions.SectionName)
        .Get<MessagingOptions>() ?? new MessagingOptions();

    builder.Services.AddMassTransit(bus =>
    {
        bus.SetKebabCaseEndpointNameFormatter();

        // Register consumers
        bus.AddConsumer<ProductCreatedConsumer>();

        switch (messagingOptions.Provider)
        {
            case MessagingProvider.RabbitMQ:
                bus.UsingRabbitMq((ctx, cfg) =>
                {
                    var rabbit = messagingOptions.RabbitMQ;
                    cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, h =>
                    {
                        h.Username(rabbit.Username);
                        h.Password(rabbit.Password);
                    });

                    // Configure receive endpoint for ProductCreatedIntegrationEvent
                    cfg.ReceiveEndpoint("product-created-queue", e =>
                    {
                        e.PrefetchCount = rabbit.PrefetchCount;
                        e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                        e.ConfigureConsumer<ProductCreatedConsumer>(ctx);
                    });
                });
                break;

            case MessagingProvider.Kafka:
                bus.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                bus.AddRider(rider =>
                {
                    rider.AddConsumer<ProductCreatedConsumer>();
                    rider.UsingKafka((ctx, k) =>
                    {
                        k.Host(messagingOptions.Kafka.BootstrapServers);
                        k.TopicEndpoint<ProductCreatedIntegrationEvent>(
                            "product-created",
                            messagingOptions.Kafka.GroupId,
                            e => e.ConfigureConsumer<ProductCreatedConsumer>(ctx));
                    });
                });
                break;

            default:
                bus.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                break;
        }
    });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Product.Consumer terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
