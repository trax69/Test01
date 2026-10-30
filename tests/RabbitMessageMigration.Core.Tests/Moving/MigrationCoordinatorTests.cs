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

    // Cuando hay una cola que migrar, entonces el informe contiene el resultado de migrar esa cola.
    [Fact]
    public async Task When_there_is_one_queue_should_report_its_migration_result()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var message = new ReceivedMessage();
        source.GetMessageCountAsync("orders", CancellationToken.None).Returns(1);
        source.ReceiveMessageAsync("orders", CancellationToken.None).Returns(message);
        var sut = new MigrationCoordinator(source, destination);

        // Act
        var report = await sut.MigrateQueuesAsync(["orders"], CancellationToken.None);

        // Assert
        Assert.Equal(1, Assert.Single(report.Results).Result.MigratedMessages);
    }

    // Cuando hay dos colas que migrar, entonces cada resultado del informe indica el nombre de su cola, en el orden indicado.
    [Fact]
    public async Task When_there_are_two_queues_should_report_each_result_with_its_queue_name()
    {
        // Arrange
        var source = Substitute.For<IMessageSource>();
        var destination = Substitute.For<IMessageDestination>();
        var sut = new MigrationCoordinator(source, destination);

        // Act
        var report = await sut.MigrateQueuesAsync(["orders", "invoices"], CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "orders", "invoices" }, report.Results.Select(entry => entry.QueueName));
    }
}
