using RabbitMessageMigration.Core.Messaging;
using RabbitMessageMigration.Core.Reporting;

namespace RabbitMessageMigration.Core.Moving;

public sealed class QueueMigration
{
    private readonly IMessageSource _messageSource;
    private readonly IMessageDestination _messageDestination;

    public QueueMigration(IMessageSource messageSource, IMessageDestination messageDestination)
    {
        _messageSource = messageSource;
        _messageDestination = messageDestination;
    }

    public async Task<QueueMigrationResult> MoveMessagesAsync(string queueName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var initialMessageCount = await _messageSource.GetMessageCountAsync(queueName, cancellationToken);
        var migratedMessages = 0;

        for (var i = 0; i < initialMessageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var message = await _messageSource.ReceiveMessageAsync(queueName, cancellationToken);

            if (message is null)
            {
                break;
            }

            try
            {
                await _messageDestination.SendMessageAsync(queueName, message, cancellationToken);
            }
            catch
            {
                await _messageSource.RequeueMessageAsync(queueName, message, cancellationToken);
                throw;
            }

            await _messageSource.ConfirmMessageAsync(queueName, message, cancellationToken);
            migratedMessages++;
        }
        return new QueueMigrationResult(migratedMessages);
    }

}
