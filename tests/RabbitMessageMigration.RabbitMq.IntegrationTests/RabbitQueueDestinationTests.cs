using System.Text;
using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.RabbitMq.IntegrationTests;

public class RabbitQueueDestinationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rabbit;

    public RabbitQueueDestinationTests(RabbitMqFixture rabbit)
    {
        _rabbit = rabbit;
    }

    // Cuando se envía un mensaje, entonces llega a la cola de destino con el mismo cuerpo.
    [Fact]
    public async Task When_message_is_sent_should_arrive_in_the_destination_queue()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var destination = new RabbitQueueDestination(connection);
        var message = new ReceivedMessage(Encoding.UTF8.GetBytes("hola"));

        // Act
        await destination.SendMessageAsync(queue, message, CancellationToken.None);

        // Assert
        Assert.Equal(1u, await _rabbit.WaitForReadyCountAsync(queue, 1));
        Assert.Equal("hola", await _rabbit.ReadBodyAsync(queue));
    }

    // Cuando el mensaje tiene propiedades, entonces llegan al destino.
    [Fact]
    public async Task When_message_has_properties_should_arrive_with_them()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var destination = new RabbitQueueDestination(connection);
        var properties = new MessageProperties(new Dictionary<string, object> { ["intentos"] = 3 })
        {
            ContentType = "application/json",
            MessageId = "id-1",
            Persistent = true
        };
        var message = new ReceivedMessage(Encoding.UTF8.GetBytes("hola"), properties);

        // Act
        await destination.SendMessageAsync(queue, message, CancellationToken.None);

        // Assert
        await _rabbit.WaitForReadyCountAsync(queue, 1);
        var received = await _rabbit.ReadPropertiesAsync(queue);
        Assert.NotNull(received);
        Assert.Equal("application/json", received.ContentType);
        Assert.Equal("id-1", received.MessageId);
        Assert.True(received.Persistent);
        Assert.Equal(3, received.Headers["intentos"]);
    }

    // Cuando se envía un mensaje, entonces no se le añaden cabeceras propias del cliente.
    [Fact]
    public async Task When_message_is_sent_should_not_add_client_headers()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var destination = new RabbitQueueDestination(connection);
        var message = new ReceivedMessage(Encoding.UTF8.GetBytes("hola"));

        // Act
        await destination.SendMessageAsync(queue, message, CancellationToken.None);

        // Assert
        await _rabbit.WaitForReadyCountAsync(queue, 1);
        var received = await _rabbit.ReadPropertiesAsync(queue);
        Assert.NotNull(received);
        Assert.Empty(received.Headers);
    }

    // Cuando la cola destino no existe, entonces SendMessageAsync falla en lugar de dar el mensaje por publicado.
    [Fact]
    public async Task When_destination_queue_does_not_exist_should_throw()
    {
        // Arrange
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var destination = new RabbitQueueDestination(connection);
        var message = new ReceivedMessage(Encoding.UTF8.GetBytes("hola"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => destination.SendMessageAsync("no-existe-" + Guid.NewGuid().ToString("N"), message, CancellationToken.None));
    }
}