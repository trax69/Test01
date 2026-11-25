using System.Text;
using RabbitMessageMigration.Core.Configuration;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

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
        await _container.StartAsync();

        // 127.0.0.1 y no "localhost": con Docker Desktop, localhost se resuelve primero a IPv6 y cada conexión tarda ~21 s.
        Settings = new RabbitSettings("127.0.0.1", _container.GetMappedPublicPort(5672), Username, Password, "/");
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

    public async Task<uint> WaitForReadyCountAsync(string queueName, uint expected)
    {
        await using var channel = await _connection.CreateChannelAsync();
        var count = 0u;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            count = (await channel.QueueDeclarePassiveAsync(queueName)).MessageCount;
            if (count == expected)
            {
                break;
            }

            await Task.Delay(50);
        }

        return count;
    }
}