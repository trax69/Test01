using RabbitMessageMigration.Core.Moving;
using RabbitMessageMigration.Core.Messaging;

using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace RabbitMessageMigration.Core.Tests.Moving;

public class QueueMigrationTests
{
    private const int OneInitialMessage = 1;

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
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
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
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
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
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
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
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        destination.SendMessageAsync("orders", message, CancellationToken.None).Returns(Task.FromException(new Exception()));
        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await source.DidNotReceive().ConfirmMessageAsync("orders", message, CancellationToken.None);
    }

    // Cuando un mensaje se mueve y el destino falla, entonces se devuelve al origen.
    [Fact]
    public async Task When_message_is_moved_and_destination_fails_should_return_to_source()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(message);

        destination.SendMessageAsync("orders", message, CancellationToken.None).Returns(Task.FromException(new Exception()));

        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await source.Received(1).RequeueMessageAsync("orders", message, CancellationToken.None);
    }

    // Cuando hay dos mensajes y aparece un tercer mensaje, entonces solo se migran los iniciales.
    [Fact]
    public async Task When_new_messages_arrive_after_migration_started_should_move_only_initial_messages()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var message1 = new ReceivedMessage();
        var message2 = new ReceivedMessage();
        var message3 = new ReceivedMessage();

        const int initialMessageCount = 2;

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(initialMessageCount);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message1, message2, message3);

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(2, result.MigratedMessages);
        await destination.Received(1).SendMessageAsync("orders", message1, CancellationToken.None);
        await destination.Received(1).SendMessageAsync("orders", message2, CancellationToken.None);
        await destination.DidNotReceive().SendMessageAsync("orders", message3, CancellationToken.None);
    }

    // Cuando falla el segundo mensaje, entonces se confirma el primero, se devuelve el segundo al origen y se detiene la migración.
    [Fact]
    public async Task When_second_message_fails_should_confirm_first_requeue_second_and_stop()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();
        var thirdMessage = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(3);

        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage, thirdMessage);

        destination.SendMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.CompletedTask);

        destination.SendMessageAsync("orders", secondMessage, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Destination unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await source.Received(1)
            .ConfirmMessageAsync("orders", firstMessage, CancellationToken.None);

        await source.Received(1)
            .RequeueMessageAsync("orders", secondMessage, CancellationToken.None);

        await destination.DidNotReceive()
            .SendMessageAsync("orders", thirdMessage, CancellationToken.None);
    }

    // Cuando falla el segundo mensaje, entonces el resultado indica que se migró 1
    [Fact]
    public async Task When_second_message_fails_should_report_one_migrated_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(2);

        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage);

        destination.SendMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.CompletedTask);

        destination.SendMessageAsync("orders", secondMessage, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Destination unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
    }

    // Cuando un mensaje es confirmado en el origen, entonces se elimina de la cola de origen.
    // Cuando hay nuevos mensajes de los contados al inicio, entonces se mueven los contados.
    // Cuando hay varios mensajes y uno falla a mitad, entonces se mueven los que se puedan y se confirma en el origen los que se movieron.
    // Cuando se cancela entre dos mensajes, entonces conserva el primero migrado y no procesa el segundo.
    [Fact]
    public async Task When_operation_is_cancelled_between_messages_should_keep_first_message_migrated_and_stop()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        source.GetMessageCountAsync("orders", cancellationToken)
            .Returns(2);

        source.ReceiveMessageAsync("orders", cancellationToken)
            .Returns(firstMessage, secondMessage);

        destination.SendMessageAsync("orders", firstMessage, cancellationToken)
            .Returns(Task.CompletedTask);

        source.When(x => x.ConfirmMessageAsync(
            "orders",
            firstMessage,
            cancellationToken))
            .Do(_ => cancellationTokenSource.Cancel());


        var sut = new QueueMigration(source, destination);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.MoveMessagesAsync("orders", cancellationTokenSource.Token));

        // Assert
        await source.Received(1)
            .ConfirmMessageAsync("orders", firstMessage, cancellationToken);

        await source.DidNotReceive()
            .ConfirmMessageAsync("orders", secondMessage, cancellationToken);

        await destination.DidNotReceive()
        .SendMessageAsync("orders", secondMessage, cancellationToken);
    }
    // Cuando se cancela la operación antes de empezar, entonces no se mueve ningún mensaje.
    [Fact]
    public async Task When_operation_is_cancelled_before_start_should_not_move_any_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var sut = new QueueMigration(source, destination);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.MoveMessagesAsync("orders", cancellationTokenSource.Token));

        // Assert
        await source.DidNotReceiveWithAnyArgs().GetMessageCountAsync(default!, default);
        await source.DidNotReceiveWithAnyArgs().ReceiveMessageAsync(default!, default);
        await destination.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    // Cuando falla el segundo mensaje, entonces el resultado indica que hubo un error.
    [Fact]
    public async Task When_second_message_fails_should_report_failure()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(2);

        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage);

        destination.SendMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.CompletedTask);

        destination.SendMessageAsync("orders", secondMessage, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Destination unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }

    // Cuando todos los mensajes se migran bien, entonces el resultado indica éxito.
    [Fact]
    public async Task When_all_messages_are_migrated_successfully_should_report_success()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    // Cuando falla el segundo mensaje, entonces el resultado conserva la excepción del destino.
    [Fact]
    public async Task When_second_message_fails_should_report_destination_exception()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();

        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(2);

        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage);
        destination.SendMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.CompletedTask);
        destination.SendMessageAsync("orders", secondMessage, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Destination unavailable")));
        
        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Equal("Destination unavailable", result.Error?.Message);
    }
}
