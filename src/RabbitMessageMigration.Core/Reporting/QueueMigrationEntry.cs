namespace RabbitMessageMigration.Core.Reporting;

public sealed class QueueMigrationEntry
{
    public QueueMigrationEntry(string queueName, QueueMigrationResult result)
    {
        QueueName = queueName;
        Result = result;
    }

    public string QueueName { get; }

    public QueueMigrationResult Result { get; }
}
