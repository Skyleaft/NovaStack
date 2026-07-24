using MassTransit;
using NovaStack.Infrastructure.Logging;
using NovaStack.Infrastructure.Messaging.Options;
using Notification.Consumer.Consumers;
using Serilog;

LoggingExtensions.BootstrapLogger();

try
{
    Log.Information("Starting Notification.Consumer...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();

    var messagingOptions = builder.Configuration
        .GetSection(MessagingOptions.SectionName)
        .Get<MessagingOptions>() ?? new MessagingOptions();

    builder.Services.AddMassTransit(bus =>
    {
        bus.SetKebabCaseEndpointNameFormatter();
        bus.AddConsumer<NotificationConsumer>();

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
                    cfg.ReceiveEndpoint("notification-queue", e =>
                    {
                        e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                        e.ConfigureConsumer<NotificationConsumer>(ctx);
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
    Log.Fatal(ex, "Notification.Consumer terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
