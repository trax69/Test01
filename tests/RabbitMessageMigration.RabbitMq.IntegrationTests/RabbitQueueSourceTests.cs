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

    // Cuando se devuelve un mensaje recibido, entonces vuelve a la cola.
    [Fact]
    public async Task When_message_is_requeued_should_return_to_the_queue()
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
        await source.RequeueMessageAsync(queue, message, CancellationToken.None);

        // Assert
        Assert.Equal(1u, await _rabbit.WaitForReadyCountAsync(queue, 1));
    }

    // Cuando se confirma dos veces el mismo mensaje, entonces la segunda lanza InvalidOperationException.
    [Fact]
    public async Task When_message_is_confirmed_twice_should_throw_invalid_operation()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await _rabbit.PublishAsync(queue, "hola");
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);
        Assert.NotNull(message);
        await source.ConfirmMessageAsync(queue, message, CancellationToken.None);

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.ConfirmMessageAsync(queue, message, CancellationToken.None));
    }

    // Cuando la cola no existe, entonces GetMessageCountAsync lanza OperationInterruptedException.
    [Fact]
    public async Task When_queue_does_not_exist_count_should_throw()
    {
        // Arrange
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            () => source.GetMessageCountAsync("no-existe-" + Guid.NewGuid().ToString("N"), CancellationToken.None));
    }

    // Cuando la cola no existe, entonces ReceiveMessageAsync lanza OperationInterruptedException.
    [Fact]
    public async Task When_queue_does_not_exist_receive_should_throw()
    {
        // Arrange
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            () => source.ReceiveMessageAsync("no-existe-" + Guid.NewGuid().ToString("N"), CancellationToken.None));
    }

    // Cuando una cola inexistente falla, entonces la siguiente cola se lee con normalidad.
    [Fact]
    public async Task When_a_queue_fails_should_still_read_the_next_queue()
    {
        // Arrange
        var queue = await _rabbit.CreateQueueAsync();
        await _rabbit.PublishAsync(queue, "hola");
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);
        await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            () => source.ReceiveMessageAsync("no-existe-" + Guid.NewGuid().ToString("N"), CancellationToken.None));

        // Act
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("hola", Encoding.UTF8.GetString(message.Body));
    }

    // Cuando la cola se crea después de fallar, entonces se lee con un canal nuevo.
    [Fact]
    public async Task When_queue_is_created_after_a_failure_should_read_it_with_a_new_channel()
    {
        // Arrange
        var queue = "creada-despues-" + Guid.NewGuid().ToString("N");
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);
        var source = new RabbitQueueSource(connection);
        await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            () => source.ReceiveMessageAsync(queue, CancellationToken.None));

        await _rabbit.CreateQueueAsync(queue);
        await _rabbit.PublishAsync(queue, "hola");
        await _rabbit.WaitForReadyCountAsync(queue, 1);

        // Act
        var message = await source.ReceiveMessageAsync(queue, CancellationToken.None);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("hola", Encoding.UTF8.GetString(message.Body));
    }
}