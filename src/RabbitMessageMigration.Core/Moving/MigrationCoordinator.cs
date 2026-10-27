using RabbitMessageMigration.Core.Messaging;
using RabbitMessageMigration.Core.Reporting;

namespace RabbitMessageMigration.Core.Moving;

public sealed class MigrationCoordinator
{
    public MigrationCoordinator(IMessageSource messageSource, IMessageDestination messageDestination)
    {
    }

    public Task<MigrationReport> MigrateQueuesAsync(IReadOnlyList<string> queueNames, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MigrationReport([]));
    }
}
