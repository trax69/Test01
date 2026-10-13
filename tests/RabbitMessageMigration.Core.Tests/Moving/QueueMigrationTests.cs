using RabbitMessageMigration.Core.Moving;
using RabbitMessageMigration.Core.Messaging;

using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace RabbitMessageMigration.Core.Tests.Moving;

public class QueueMigrationTests
{
    // Cuando la cola está vacía, entonces el resultado indica que se han migrado 0 mensajes.
    [Fact]
    public async Task When_queue_is_empty_should_report_zero_migrated_messages()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        source.ReceiveMessageAsync("orders", CancellationToken.None).ReturnsNull();
        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(0, result.MigratedMessages);
    }

    // Cuando la cola está vacía, entonces no se envía ningún mensaje al destino.
    [Fact]
    public async Task When_queue_is_empty_should_not_send_any_message_to_destination()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        source.ReceiveMessageAsync("orders", CancellationToken.None).ReturnsNull();
        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await destination.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    // Cuando la cola tiene un mensaje, entonces se envía ese mismo mensaje al destino.
    [Fact]
    public async Task When_queue_has_one_message_should_send_that_message_to_destination()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await destination.Received(1).SendMessageAsync("orders", message, CancellationToken.None);
    }

    // Cuando la cola tiene un mensaje, entonces el resultado indica que se ha migrado 1 mensaje.
    [Fact]
    public async Task When_queue_has_one_message_should_report_one_migrated_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
    }

    // Cuando un mensaje es movido entonces se confirma en el origen.
    [Fact]
    public async Task When_message_is_moved_should_confirm_in_source()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await source.Received(1).ConfirmMessageAsync("orders", message, CancellationToken.None);

    }

    // Cuando el destino falla, entonces no se confirma el mensaje en el origen.
    [Fact]
    public async Task When_destination_fails_should_not_confirm_message_in_source()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        destination.SendMessageAsync("orders", message, CancellationToken.None).Returns(Task.FromException(new Exception()));
        var sut = new QueueMigration(source, destination);

        // Act
        await Assert.ThrowsAsync<Exception>(() => sut.MoveMessagesAsync("orders", CancellationToken.None));

        // Assert
        await source.DidNotReceive().ConfirmMessageAsync("orders", message, CancellationToken.None);
    }

    // Cuando un mensaje es confirmado en el origen, entonces se elimina de la cola de origen.
    // Cuando un mensaje se mueve y el destino falla, entonces se devuelve al origen.
    // Cuando hay nuevos mensajes de los contados al inicio, entonces se mueven los contados.
    // Cuando hay varios mensajes y uno falla a mitad, entonces se mueven los que se puedan y se confirma en el origen los que se movieron.
    // Cuando se cancela la operación, entonces se detiene el movimiento y no se mueve ningún mensaje.
    // Cuando se cancela a mitad de la operación, entonces se detiene el movimiento y no se mueve ningún mensaje.

}