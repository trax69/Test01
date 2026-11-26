using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

public sealed class RabbitQueueDestination : IMessageDestination
{
    private readonly IConnection _connection;

    public RabbitQueueDestination(IConnection connection)
    {
        _connection = connection;
    }

    public async Task SendMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken)
    {
        // Confirmaciones activadas pero sin seguimiento.esto añade una cabecera al mensaje.
        await using var channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: false),
            cancellationToken);

        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? failure = null;

        // El retorno de un mensaje siempre llega antes de su confirmación, porque se publica de uno en uno.
        channel.BasicReturnAsync += (_, args) =>
        {
            failure = new InvalidOperationException(
                $"RabbitMQ devolvió el mensaje de la cola '{queueName}': {args.ReplyCode} {args.ReplyText}.");
            return Task.CompletedTask;
        };
        channel.BasicAcksAsync += (_, _) =>
        {
            confirmation.TrySetResult();
            return Task.CompletedTask;
        };
        channel.BasicNacksAsync += (_, _) =>
        {
            failure ??= new InvalidOperationException($"RabbitMQ rechazó el mensaje de la cola '{queueName}'.");
            confirmation.TrySetResult();
            return Task.CompletedTask;
        };

        await channel.BasicPublishAsync(
            "", queueName, mandatory: true, RabbitMessageMapper.ToBasicProperties(message.Properties), message.Body, cancellationToken);

        await confirmation.Task.WaitAsync(cancellationToken);

        if (failure is not null)
        {
            throw failure;
        }
    }
}