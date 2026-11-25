using System.Text;
using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace RabbitMessageMigration.RabbitMq.IntegrationTests;

public class RabbitQueueSourceTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rabbit;

    public RabbitQueueSourceTests(RabbitMqFixture rabbit)
    {
        _rabbit = rabbit;
    }

    // Cuando la cola tiene mensajes, entonces GetMessageCountAsync devuelve cuántos hay.
    [Fact]
    public async Task When_queue_has_messages_should_return_the_message_count()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await _rabbit.PublishAsync(queue, "uno");
        await _rabbit.PublishAsync(queue, "dos");
        await _rabbit.PublishAsync(queue, "tres");
        await _rabbit.WaitForReadyCountAsync(queue, 3);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act
        var count = await source.GetMessageCountAsync(queue, CancellationToken.None);

        // Assert
        Assert.Equal(3, count);
    }
}