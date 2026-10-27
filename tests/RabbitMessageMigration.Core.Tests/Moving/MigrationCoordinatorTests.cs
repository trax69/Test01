using RabbitMessageMigration.Core.Messaging;
using RabbitMessageMigration.Core.Moving;

using NSubstitute;

namespace RabbitMessageMigration.Core.Tests.Moving;

public class MigrationCoordinatorTests
{
    // Cuando no hay colas que migrar, entonces el informe no contiene resultados.
    [Fact]
    public async Task When_there_are_no_queues_should_report_no_results()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var sut = new MigrationCoordinator(source, destination);

        // Act
        var report = await sut.MigrateQueuesAsync([], CancellationToken.None);

        // Assert
        Assert.Empty(report.Results);
    }
}
