using RabbitMessageMigration.Core.Configuration;
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

    public RabbitSettings Settings { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // 127.0.0.1 y no "localhost": con Docker Desktop, localhost se resuelve primero a IPv6 y cada conexión tarda ~21 s.
        Settings = new RabbitSettings("127.0.0.1", _container.GetMappedPublicPort(5672), Username, Password, "/");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}