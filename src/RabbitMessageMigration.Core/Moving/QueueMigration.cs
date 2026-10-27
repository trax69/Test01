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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new QueueMigrationResult(0, ex);
        }

        var migratedMessages = 0;
        Exception? error = null;

        for (var i = 0; i < initialMessageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReceivedMessage? message;
            try
            {
                message = await _messageSource.ReceiveMessageAsync(queueName, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex;
                break;
            }

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
                try
                {
                    await _messageSource.RequeueMessageAsync(queueName, message, cancellationToken);
                }
                catch (Exception requeueException)
                {
                    error = new AggregateException(ex, requeueException);
                }

                break;
            }

            try
            {
                await _messageSource.ConfirmMessageAsync(queueName, message, CancellationToken.None);
            }
            catch (Exception ex)
            {
                error = ex;
                break;
            }

            migratedMessages++;
        }
        return new QueueMigrationResult(migratedMessages, error);
    }

}
