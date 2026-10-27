using RabbitMessageMigration.Core.Moving;
using RabbitMessageMigration.Core.Messaging;

using NSubstitute;

namespace RabbitMessageMigration.Core.Tests.Moving;

public class QueueMigrationTests
{
    private const int NoInitialMessages = 0;
    private const int OneInitialMessage = 1;

    // Cuando la cola está vacía, entonces el resultado indica que se han migrado 0 mensajes.
    [Fact]
    public async Task When_queue_is_empty_should_report_zero_migrated_messages()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(NoInitialMessages);
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
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(NoInitialMessages);
        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await destination.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
    }

    // Cuando el origen se queda sin mensajes antes de lo previsto, entonces el resultado indica solo los mensajes migrados.
    [Fact]
    public async Task When_source_runs_out_of_messages_before_initial_count_should_report_only_migrated_messages()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var firstMessage = new ReceivedMessage();
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(2);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(firstMessage, (ReceivedMessage?)null);
        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
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

        destination.SendMessageAsync("orders", firstMessage, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        source.When(x => x.ConfirmMessageAsync(
            "orders",
            firstMessage,
            Arg.Any<CancellationToken>()))
            .Do(_ => cancellationTokenSource.Cancel());


        var sut = new QueueMigration(source, destination);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.MoveMessagesAsync("orders", cancellationTokenSource.Token));

        // Assert
        await source.Received(1)
            .ConfirmMessageAsync("orders", firstMessage, Arg.Any<CancellationToken>());

        await source.DidNotReceive()
            .ConfirmMessageAsync("orders", secondMessage, Arg.Any<CancellationToken>());

        await destination.DidNotReceive()
            .SendMessageAsync("orders", secondMessage, Arg.Any<CancellationToken>());
    }

    // Cuando se cancela mientras se envía un mensaje, entonces ese mensaje se termina de migrar.
    [Fact]
    public async Task When_operation_is_cancelled_while_sending_message_should_finish_migrating_that_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        var cancellationTokenSource = new CancellationTokenSource();

        source.GetMessageCountAsync("orders", Arg.Any<CancellationToken>()).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", Arg.Any<CancellationToken>()).Returns(message);

        // El destino respeta el token que recibe, como haría el cliente real de RabbitMQ.
        destination.SendMessageAsync("orders", message, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellationTokenSource.Cancel();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", cancellationTokenSource.Token);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
    }

    // Cuando se cancela después de enviar un mensaje y antes de confirmarlo, entonces ese mensaje se termina de migrar.
    [Fact]
    public async Task When_operation_is_cancelled_after_sending_message_should_finish_migrating_that_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        var cancellationTokenSource = new CancellationTokenSource();

        source.GetMessageCountAsync("orders", Arg.Any<CancellationToken>()).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", Arg.Any<CancellationToken>()).Returns(message);

        destination.SendMessageAsync("orders", message, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.CompletedTask;
            });

        // El origen respeta el token que recibe, como haría el cliente real de RabbitMQ.
        source.ConfirmMessageAsync("orders", message, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", cancellationTokenSource.Token);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
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
        var exception = new InvalidOperationException("Destination unavailable");

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(2);

        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage);

        destination.SendMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.CompletedTask);
        destination.SendMessageAsync("orders", secondMessage, CancellationToken.None)
            .Returns(Task.FromException(exception));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Same(exception, result.Error);
    }

    // Cuando falla al contar los mensajes del origen, entonces el resultado conserva la excepción del origen.
    [Fact]
    public async Task When_counting_messages_fails_should_report_source_exception()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var exception = new InvalidOperationException("Queue does not exist");

        source.GetMessageCountAsync("orders", CancellationToken.None)
            .Returns(Task.FromException<int>(exception));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Same(exception, result.Error);
    }

    // Cuando se cancela mientras se cuentan los mensajes del origen, entonces se lanza OperationCanceledException.
    [Fact]
    public async Task When_operation_is_cancelled_while_counting_messages_should_throw_operation_cancelled()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var cancellationTokenSource = new CancellationTokenSource();

        // El origen respeta el token que recibe, como haría el cliente real de RabbitMQ.
        source.GetMessageCountAsync("orders", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellationTokenSource.Cancel();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult(OneInitialMessage);
            });

        var sut = new QueueMigration(source, destination);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.MoveMessagesAsync("orders", cancellationTokenSource.Token));
    }

    // Cuando falla al recibir un mensaje del origen, entonces el resultado conserva la excepción del origen.
    [Fact]
    public async Task When_receiving_message_fails_should_report_source_exception()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var exception = new InvalidOperationException("Source unavailable");

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(Task.FromException<ReceivedMessage?>(exception));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Same(exception, result.Error);
    }

    // Cuando falla al recibir el segundo mensaje del origen, entonces el resultado indica que se migró 1.
    [Fact]
    public async Task When_receiving_second_message_fails_should_report_one_migrated_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var firstMessage = new ReceivedMessage();
        var exception = new InvalidOperationException("Source unavailable");

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(2);
        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(
                Task.FromResult<ReceivedMessage?>(firstMessage),
                Task.FromException<ReceivedMessage?>(exception));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(1, result.MigratedMessages);
    }

    // Cuando se cancela mientras se recibe un mensaje del origen, entonces se lanza OperationCanceledException.
    [Fact]
    public async Task When_operation_is_cancelled_while_receiving_message_should_throw_operation_cancelled()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var cancellationTokenSource = new CancellationTokenSource();

        source.GetMessageCountAsync("orders", Arg.Any<CancellationToken>()).Returns(OneInitialMessage);

        // El origen respeta el token que recibe, como haría el cliente real de RabbitMQ.
        source.ReceiveMessageAsync("orders", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellationTokenSource.Cancel();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult<ReceivedMessage?>(new ReceivedMessage());
            });

        var sut = new QueueMigration(source, destination);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.MoveMessagesAsync("orders", cancellationTokenSource.Token));
    }

    // Cuando falla al confirmar un mensaje en el origen, entonces el resultado conserva la excepción del origen.
    [Fact]
    public async Task When_confirming_message_fails_should_report_source_exception()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        var exception = new InvalidOperationException("Source unavailable");

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        source.ConfirmMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(exception));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Same(exception, result.Error);
    }

    // Cuando el destino falla y también falla al devolver el mensaje al origen, entonces el resultado indica que hubo un error.
    [Fact]
    public async Task When_destination_fails_and_requeue_fails_should_report_failure()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        destination.SendMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Destination unavailable")));
        source.RequeueMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Source unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }

    // Cuando el destino falla y también falla al devolver el mensaje al origen, entonces el resultado conserva ambas excepciones.
    [Fact]
    public async Task When_destination_fails_and_requeue_fails_should_report_both_exceptions()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        var destinationException = new InvalidOperationException("Destination unavailable");
        var requeueException = new InvalidOperationException("Source unavailable");

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        destination.SendMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(destinationException));
        source.RequeueMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(requeueException));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        var aggregateException = Assert.IsType<AggregateException>(result.Error);
        Assert.Equal(new Exception[] { destinationException, requeueException }, aggregateException.InnerExceptions);
    }

    // Cuando falla al confirmar un mensaje en el origen, entonces ese mensaje no se cuenta como migrado.
    [Fact]
    public async Task When_confirming_message_fails_should_not_count_it_as_migrated()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(OneInitialMessage);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        source.ConfirmMessageAsync("orders", message, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Source unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        var result = await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        Assert.Equal(0, result.MigratedMessages);
    }

    // Cuando falla al confirmar el primer mensaje, entonces no se envía el segundo mensaje al destino.
    [Fact]
    public async Task When_confirming_first_message_fails_should_not_send_second_message()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();
        var thirdMessage = new ReceivedMessage();

        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(3);
        source.ReceiveMessageAsync("orders", CancellationToken.None)
            .Returns(firstMessage, secondMessage, thirdMessage);
        source.ConfirmMessageAsync("orders", firstMessage, CancellationToken.None)
            .Returns(Task.FromException(new InvalidOperationException("Source unavailable")));

        var sut = new QueueMigration(source, destination);

        // Act
        await sut.MoveMessagesAsync("orders", CancellationToken.None);

        // Assert
        await destination.DidNotReceive()
            .SendMessageAsync("orders", secondMessage, Arg.Any<CancellationToken>());
    }
}
