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

        int initialMessageCount;
        try
        {
            initialMessageCount = await _messageSource.GetMessageCountAsync(queueName, cancellationToken);
        }
        catch (Exception ex)
        {
            return new QueueMigrationResult(0, ex);
        }

        var migratedMessages = 0;
        Exception? error = null;

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
                await _messageDestination.SendMessageAsync(queueName, message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                error = ex;
                await _messageSource.RequeueMessageAsync(queueName, message, cancellationToken);
                break;
            }

            await _messageSource.ConfirmMessageAsync(queueName, message, CancellationToken.None);
            migratedMessages++;
        }
        return new QueueMigrationResult(migratedMessages, error);
    }

}
