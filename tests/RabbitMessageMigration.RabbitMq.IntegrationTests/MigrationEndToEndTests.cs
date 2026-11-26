using System.Text;
using RabbitMessageMigration.Core.Moving;

namespace RabbitMessageMigration.RabbitMq.IntegrationTests;

// Dos Rabbits distintos, 2 contenedores docker .
public class MigrationEndToEndTests : IAsyncLifetime
{
    private readonly RabbitMqFixture _origin = new();
    private readonly RabbitMqFixture _destination = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_origin.InitializeAsync(), _destination.InitializeAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_origin.DisposeAsync(), _destination.DisposeAsync());
    }

    // Cuando la cola tiene mensajes, entonces se mueven al destino en el mismo orden y el origen queda vacío.
    [Fact]
    public async Task When_queue_has_messages_should_move_them_in_order_to_the_destination()
    {
        // Arrange
        var queue = await _origin.CreateQueueAsync();
        await _destination.CreateQueueAsync(queue);
        await _origin.PublishAsync(queue, "uno");
        await _origin.PublishAsync(queue, "dos");
        await _origin.PublishAsync(queue, "tres");
        await _origin.WaitForReadyCountAsync(queue, 3);

        await using var originConnection = await RabbitConnector.ConnectAsync(_origin.Settings);
        await using var destinationConnection = await RabbitConnector.ConnectAsync(_destination.Settings);
        var source = new RabbitQueueSource(originConnection);
        var coordinator = new MigrationCoordinator(source, new RabbitQueueDestination(destinationConnection));

        // Act
        var report = await coordinator.MigrateQueuesAsync([queue], CancellationToken.None);
        await source.DisposeAsync(); // si quedara algún mensaje sin confirmar, volvería a la cola de origen

        // Assert
        var entry = Assert.Single(report.Results);
        Assert.True(entry.Result.IsSuccess);
        Assert.Equal(3, entry.Result.MigratedMessages);
        Assert.Equal(0u, await _origin.WaitForReadyCountAsync(queue, 0));
        Assert.Equal(3u, await _destination.WaitForReadyCountAsync(queue, 3));
        Assert.Equal("uno", await _destination.ReadBodyAsync(queue));
        Assert.Equal("dos", await _destination.ReadBodyAsync(queue));
        Assert.Equal("tres", await _destination.ReadBodyAsync(queue));
    }

    // Cuando la cola no existe en el destino, entonces la migración de esa cola falla y los mensajes siguen en el origen.
    [Fact]
    public async Task When_destination_queue_does_not_exist_should_fail_and_keep_the_messages_in_the_origin()
    {
        // Arrange
        var queue = await _origin.CreateQueueAsync(); // a propósito, no se crea en el destino
        await _origin.PublishAsync(queue, "uno");
        await _origin.PublishAsync(queue, "dos");
        await _origin.WaitForReadyCountAsync(queue, 2);

        await using var originConnection = await RabbitConnector.ConnectAsync(_origin.Settings);
        await using var destinationConnection = await RabbitConnector.ConnectAsync(_destination.Settings);
        var source = new RabbitQueueSource(originConnection);
        var coordinator = new MigrationCoordinator(source, new RabbitQueueDestination(destinationConnection));

        // Act
        var report = await coordinator.MigrateQueuesAsync([queue], CancellationToken.None);
        await source.DisposeAsync();

        // Assert
        var entry = Assert.Single(report.Results);
        Assert.False(entry.Result.IsSuccess);
        Assert.Equal(0, entry.Result.MigratedMessages);
        Assert.IsType<InvalidOperationException>(entry.Result.Error);
        Assert.Equal(2u, await _origin.WaitForReadyCountAsync(queue, 2));
    }
}