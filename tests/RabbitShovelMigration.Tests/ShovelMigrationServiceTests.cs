using RabbitShovelMigration.Core;

namespace RabbitShovelMigration.Tests;

public class ShovelMigrationServiceTests
{
    private static readonly RabbitEndpointConfig Source = new(
        ManagementHost: "localhost", ManagementPort: 9090,
        AmqpHost: "rabbitA", AmqpPort: 5672,
        VirtualHost: "/", Username: "root", Password: "root");

    private static readonly RabbitEndpointConfig Destination = new(
        ManagementHost: "localhost", ManagementPort: 9191,
        AmqpHost: "rabbitB", AmqpPort: 5672,
        VirtualHost: "/", Username: "root", Password: "root");

    [Fact]
    public async Task MigrateQueueAsync_QueueMissingOnSource_ReturnsFailureWithoutCreatingShovel()
    {
        var client = new FakeRabbitManagementClient();
        var service = new ShovelMigrationService(client);

        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.False(result.Success);
        Assert.Contains("no existe", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(client.CreatedShovels);
    }

    [Fact]
    public async Task MigrateQueueAsync_EmptyQueue_ClonesStructureAndSkipsShovel()
    {
        var client = new FakeRabbitManagementClient();
        var arguments = new Dictionary<string, object?> { ["x-message-ttl"] = 3_600_000L };
        client.SetQueue(Source, new QueueInfo("test", Durable: true, AutoDelete: true, arguments, MessagesReady: 0));
        var service = new ShovelMigrationService(client);

        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.True(result.Success);
        Assert.Equal(0, result.MessagesBeforeMigration);
        Assert.Equal(0, result.MessagesAfterMigration);
        Assert.Empty(client.CreatedShovels);

        var clonedQueue = await client.GetQueueAsync(Destination, "test");
        Assert.NotNull(clonedQueue);
        Assert.True(clonedQueue!.Durable);
        Assert.True(clonedQueue.AutoDelete);
        Assert.Equal(3_600_000L, clonedQueue.Arguments["x-message-ttl"]);
    }

    [Fact]
    public async Task MigrateQueueAsync_QueueWithPendingMessages_UsesQueueLengthShovelAndReportsSuccess()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", Durable: true, AutoDelete: false, new Dictionary<string, object?>(), MessagesReady: 5));

        client.OnShovelCreated = definition =>
        {
            // Simulate the shovel instantly draining the 5 pending messages across.
            client.SetQueue(Destination, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 5));
            client.SetShovelStatus(Source, "/", definition.Name, new ShovelStatus(definition.Name, ShovelState.Terminated, "Shovel finished"));
        };

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.True(result.Success);
        Assert.Equal(5, result.MessagesBeforeMigration);
        Assert.Equal(5, result.MessagesAfterMigration);

        var shovel = Assert.Single(client.CreatedShovels);
        Assert.Equal("test", shovel.SourceQueue);
        Assert.Equal("test", shovel.DestinationQueue);
        Assert.Equal(Source.ToAmqpUri(), shovel.SourceUri);
        Assert.Equal(Destination.ToAmqpUri(), shovel.DestinationUri);
        Assert.Equal("queue-length", shovel.SourceDeleteAfter);
    }

    [Fact]
    public async Task MigrateQueueAsync_ShovelTerminatesWithError_ReturnsFailureWithReason()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 3));

        client.OnShovelCreated = definition =>
            client.SetShovelStatus(Source, "/", definition.Name, new ShovelStatus(definition.Name, ShovelState.Terminated, "authentication failure"));

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.False(result.Success);
        Assert.Equal("authentication failure", result.ErrorMessage);
    }

    [Fact]
    public async Task MigrateQueueAsync_ShovelNeverTerminates_TimesOut()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 2));

        client.OnShovelCreated = definition =>
            client.SetShovelStatus(Source, "/", definition.Name, new ShovelStatus(definition.Name, ShovelState.Running, Reason: null));

        var service = new ShovelMigrationService(
            client,
            pollInterval: TimeSpan.FromMilliseconds(2),
            timeout: TimeSpan.FromMilliseconds(20),
            delay: (_, _) => Task.CompletedTask);

        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.False(result.Success);
        Assert.Contains("Tiempo de espera agotado", result.ErrorMessage);
    }

    [Fact]
    public async Task MigrateQueueAsync_FewerMessagesArrivedThanExpected_ReturnsFailure()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 5));

        client.OnShovelCreated = definition =>
        {
            client.SetQueue(Destination, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 3));
            client.SetShovelStatus(Source, "/", definition.Name, new ShovelStatus(definition.Name, ShovelState.Terminated, "Shovel finished"));
        };

        var service = new ShovelMigrationService(
            client,
            pollInterval: TimeSpan.FromMilliseconds(2),
            timeout: TimeSpan.FromMilliseconds(20),
            delay: (_, _) => Task.CompletedTask);
        var result = await service.MigrateQueueAsync(Source, Destination, "test");

        Assert.False(result.Success);
        Assert.Contains("3", result.ErrorMessage);
        Assert.Contains("5", result.ErrorMessage);
    }

    private static QueueMessage Msg(string body) => new(body, "string", default);

    [Fact]
    public async Task MigrateQueueAsync_KeepInSource_CopiesMessagesWithoutShovelAndLeavesSourceUntouched()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 3));
        client.SetMessages(Source, "test", new[] { Msg("a"), Msg("b"), Msg("c") });

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test", keepMessagesInSource: true);

        Assert.True(result.Success);
        Assert.Equal(3, result.MessagesBeforeMigration);
        Assert.Equal(3, result.MessagesAfterMigration);
        Assert.Empty(client.CreatedShovels);
        Assert.Equal(new[] { "a", "b", "c" }, client.PublishedMessages.Select(p => p.Message.Payload));
        Assert.All(client.PublishedMessages, p => Assert.Equal(Destination.ManagementPort, p.ManagementPort));

        var sourceQueue = await client.GetQueueAsync(Source, "test");
        Assert.Equal(3, sourceQueue!.MessagesReady);
    }

    [Fact]
    public async Task MigrateQueueAsync_KeepInSource_DestinationAlreadyHasMessages_ExpectsCountOnTopOfExisting()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 2));
        client.SetQueue(Destination, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 10));
        client.SetMessages(Source, "test", new[] { Msg("a"), Msg("b") });

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test", keepMessagesInSource: true);

        Assert.True(result.Success);
        Assert.Equal(12, result.MessagesAfterMigration);
    }

    [Fact]
    public async Task MigrateQueueAsync_KeepInSource_FewerMessagesPeekedThanExpected_FailsWithoutPublishing()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 3));
        client.SetMessages(Source, "test", new[] { Msg("a") });

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test", keepMessagesInSource: true);

        Assert.False(result.Success);
        Assert.Empty(client.PublishedMessages);
    }

    [Fact]
    public async Task MigrateQueueAsync_KeepInSource_EmptyQueue_ClonesStructureOnly()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("test", true, false, new Dictionary<string, object?>(), MessagesReady: 0));

        var service = new ShovelMigrationService(client);
        var result = await service.MigrateQueueAsync(Source, Destination, "test", keepMessagesInSource: true);

        Assert.True(result.Success);
        Assert.Empty(client.PublishedMessages);
        Assert.NotNull(await client.GetQueueAsync(Destination, "test"));
    }

    [Fact]
    public async Task MigrateAsync_MultipleQueues_AggregatesAllResultsInOrder()
    {
        var client = new FakeRabbitManagementClient();
        client.SetQueue(Source, new QueueInfo("orders", true, false, new Dictionary<string, object?>(), MessagesReady: 0));
        // "missing-queue" intentionally not registered on the source.

        var service = new ShovelMigrationService(client);
        var report = await service.MigrateAsync(Source, Destination, new[] { "orders", "missing-queue" });

        Assert.Equal(2, report.Results.Count);
        Assert.Equal("orders", report.Results[0].QueueName);
        Assert.True(report.Results[0].Success);
        Assert.Equal("missing-queue", report.Results[1].QueueName);
        Assert.False(report.Results[1].Success);
        Assert.False(report.AllSucceeded);
    }
}
