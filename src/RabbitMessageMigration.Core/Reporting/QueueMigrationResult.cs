namespace RabbitMessageMigration.Core.Reporting;

public sealed class QueueMigrationResult
{
    public QueueMigrationResult(int migratedMessages)
    {

        MigratedMessages = migratedMessages;
    }


    public int MigratedMessages { get; }

}