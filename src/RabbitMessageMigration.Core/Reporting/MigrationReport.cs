namespace RabbitMessageMigration.Core.Reporting;

public sealed class MigrationReport
{
    public MigrationReport(IReadOnlyList<QueueMigrationEntry> results)
    {
        Results = results;
    }

    public IReadOnlyList<QueueMigrationEntry> Results { get; }
}

