namespace RabbitMessageMigration.Core.Reporting;

public sealed class QueueMigrationResult
{
    public QueueMigrationResult(int migratedMessages, Exception? error = null)
    {
        MigratedMessages = migratedMessages;
        Error = error;
    }

    public int MigratedMessages { get; }

    public Exception? Error { get; }

    public bool IsSuccess => Error is null;
}
