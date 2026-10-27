namespace RabbitMessageMigration.Core.Reporting;

public sealed class MigrationReport
{
    public MigrationReport(IReadOnlyList<QueueMigrationResult> results)
    {
        Results = results;
    }

    public IReadOnlyList<QueueMigrationResult> Results { get; }
}

