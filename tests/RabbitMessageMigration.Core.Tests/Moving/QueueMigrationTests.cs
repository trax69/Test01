using RabbitMessageMigration.Core.Moving;
using RabbitMessageMigration.Core.Messaging;

using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace RabbitMessageMigration.Core.Tests.Moving;

public class QueueMigrationTests
{


    // Cuando la cola tiene un mensaje, entonces se mueve el mensaje al destino.
    // Cuando un mensaje es movido entonces se confirma en el origen.
    // Cuando un mensaje es confirmado en el origen, entonces se elimina de la cola de origen.

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

    // Cuando un mensaje se mueve y el destino falla, entonces se devuelve al origen.
    // Cuando hay nuevos mensajes de los contados al inicio, entonces se mueven los contados.
    // Cuando hay varios mensajes y uno falla a mitad, entonces se mueven los que se puedan y se confirma en el origen los que se movieron.
    // Cuando se cancela la operación, entonces se detiene el movimiento y no se mueve ningún mensaje.
    // Cuando se cancela a mitad de la operación, entonces se detiene el movimiento y no se mueve ningún mensaje.

}