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

    public Task<QueueMigrationResult> MoveMessagesAsync(string queueName, CancellationToken cancellationToken)
    {
        return Task.FromResult(new QueueMigrationResult(0));
    }

}