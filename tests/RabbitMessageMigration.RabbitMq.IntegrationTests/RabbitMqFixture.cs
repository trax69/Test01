using System.Text;
using RabbitMessageMigration.Core.Configuration;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.RabbitMq.IntegrationTests;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private const string Username = "migration";
    private const string Password = "migration";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4-alpine")
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    private IConnection _connection = null!;

    public RabbitSettings Settings { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var AmqpPort = 5672;
        await _container.StartAsync();

        // 127.0.0.1 y no "localhost": con Docker Desktop, localhost se resuelve primero a IPv6 y cada conexión tarda ~21 s.
        Settings = new RabbitSettings("127.0.0.1", _container.GetMappedPublicPort(AmqpPort), Username, Password, "/");
        _connection = await RabbitConnector.ConnectAsync(Settings);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    // RabbitMQ 4 no admite colas no duraderas y no exclusivas
    public async Task<string> CreateQueueAsync(string? queueName = null)
    {
        queueName ??= "test-" + Guid.NewGuid().ToString("N");
        await using var channel = await _connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
        return queueName;
    }

    public async Task PublishAsync(string queueName, string body, BasicProperties? properties = null)
    {
        await using var channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
        await channel.BasicPublishAsync("", queueName, mandatory: true, properties ?? new BasicProperties(), Encoding.UTF8.GetBytes(body));
    }

    public async Task<string?> ReadBodyAsync(string queueName)
    {
        await using var channel = await _connection.CreateChannelAsync();
        var result = await channel.BasicGetAsync(queueName, autoAck: true);
        return result is null ? null : Encoding.UTF8.GetString(result.Body.Span);
    }

    public async Task<MessageProperties?> ReadPropertiesAsync(string queueName)
    {
        await using var channel = await _connection.CreateChannelAsync();
        var result = await channel.BasicGetAsync(queueName, autoAck: true);
        if (result is null)
        {
            return null;
        }

        var headers = new Dictionary<string, object>();
        foreach (var (key, value) in result.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            if (value is not null)
            {
                headers[key] = value;
            }
        }

        return new MessageProperties(headers)
        {
            ContentType = result.BasicProperties.ContentType,
            MessageId = result.BasicProperties.MessageId,
            Persistent = result.BasicProperties.DeliveryMode == DeliveryModes.Persistent
        };
    }

    public async Task<uint> WaitForReadyCountAsync(string queueName, uint expected)
    {
        await using var channel = await _connection.CreateChannelAsync();
        var count = 0u;
        var MaxAttempts = 100;
        var MillisecondsDelay = 50;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            count = (await channel.QueueDeclarePassiveAsync(queueName)).MessageCount;
            if (count == expected)
            {
                break;
            }

            await Task.Delay(MillisecondsDelay);
        }

        return count;
    }
}