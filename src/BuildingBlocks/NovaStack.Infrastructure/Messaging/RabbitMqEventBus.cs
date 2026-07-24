using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaStack.Contracts.IntegrationEvents;
using NovaStack.Infrastructure.Messaging.Options;
using RabbitMQ.Client;

namespace NovaStack.Infrastructure.Messaging;

/// <summary>
/// A lightweight, custom implementation of <see cref="IEventBus"/> using the native <see cref="RabbitMQ.Client"/>.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqEventBus(IOptions<MessagingOptions> options)
    {
        _options = options.Value.RabbitMQ;
    }

    private void EnsureConnectionAndChannel()
    {
        if (_channel is { IsOpen: true }) return;

        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.Username,
                Password = _options.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
    }

    public Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) 
        where T : class, IIntegrationEvent
    {
        return PublishAsync(integrationEvent, null, ct);
    }

    public Task PublishAsync<T>(T integrationEvent, string? topicOrExchange, CancellationToken ct = default) 
        where T : class, IIntegrationEvent
    {
        EnsureConnectionAndChannel();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(integrationEvent));

        // Use event name (e.g. "ProductCreatedIntegrationEvent") as queue/routing key name if none specified
        var targetName = topicOrExchange ?? typeof(T).Name;

        // Declare queue first to ensure it exists if there's no exchange setup yet
        _channel!.QueueDeclare(
            queue: targetName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: targetName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
