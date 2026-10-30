using RabbitMessageMigration.Core.Messaging;
using RabbitMessageMigration.Core.Reporting;

namespace RabbitMessageMigration.Core.Moving;

public sealed class MigrationCoordinator
{
    private readonly QueueMigration _queueMigration;

    public MigrationCoordinator(IMessageSource messageSource, IMessageDestination messageDestination)
    {
        _queueMigration = new QueueMigration(messageSource, messageDestination);
    }

    public async Task<MigrationReport> MigrateQueuesAsync(IReadOnlyList<string> queueNames, CancellationToken cancellationToken)
    {
        var results = new List<QueueMigrationEntry>();

        foreach (var queueName in queueNames)
        {
            results.Add(new QueueMigrationEntry(queueName, await _queueMigration.MoveMessagesAsync(queueName, cancellationToken)));
        }

        return new MigrationReport(results);
    }
}
