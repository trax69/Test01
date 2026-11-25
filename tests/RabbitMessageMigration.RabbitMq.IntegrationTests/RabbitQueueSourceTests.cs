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

    // Cuando la cola está vacía, entonces ReceiveMessageAsync devuelve null.
    [Fact]
    public async Task When_queue_is_empty_should_return_null()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);

        // Assert
        Assert.Null(message);
    }

    // Cuando la cola tiene un mensaje, entonces ReceiveMessageAsync devuelve su cuerpo.
    [Fact]
    public async Task When_queue_has_a_message_should_return_its_body()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await _rabbit.PublishAsync(queue, "hola");
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("hola", Encoding.UTF8.GetString(message.Body));
    }

    // Cuando el mensaje tiene propiedades, entonces ReceiveMessageAsync las devuelve mapeadas.
    [Fact]
    public async Task When_message_has_properties_should_return_them_mapped()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = "id-1",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?> { ["intentos"] = 3 }
        };
        await _rabbit.PublishAsync(queue, "hola", properties);
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("application/json", message.Properties.ContentType);
        Assert.Equal("id-1", message.Properties.MessageId);
        Assert.True(message.Properties.Persistent);
        Assert.Equal(3, message.Properties.Headers["intentos"]);
    }

    // Cuando se confirma un mensaje recibido, entonces no vuelve a la cola al cerrar la conexión.
    [Fact]
    public async Task When_message_is_confirmed_should_not_return_to_the_queue()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await _rabbit.PublishAsync(queue, "hola");
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);
        Assert.NotNull(message);

        // Act
        await source.ConfirmMessageAsync(queue, message, CancellationToken.None);
        await connection.CloseAsync(); // sin confirmar, RabbitMQ lo devolvería a la cola

        // Assert
        Assert.Equal(0u, await _rabbit.WaitForReadyCountAsync(queue, 0));
    }
}