namespace RabbitShovelMigration.Core;

/// <summary>The subset of the RabbitMQ HTTP Management API the migrator needs; abstracted so the service can be tested without a broker.</summary>
public interface IRabbitManagementClient
{
    Task<QueueInfo?> GetQueueAsync(RabbitEndpointConfig endpoint, string queueName, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListQueueNamesAsync(RabbitEndpointConfig endpoint, CancellationToken ct = default);

    Task DeclareQueueAsync(RabbitEndpointConfig endpoint, QueueInfo queue, CancellationToken ct = default);

    /// <summary>Reads up to <paramref name="count"/> messages without removing them (get + ack_requeue_true); they stay queued, flagged as redelivered.</summary>
    Task<IReadOnlyList<QueueMessage>> PeekMessagesAsync(RabbitEndpointConfig endpoint, string queueName, long count, CancellationToken ct = default);

    /// <summary>Publishes a peeked message via the default exchange. Returns false if it could not be routed.</summary>
    Task<bool> PublishMessageAsync(RabbitEndpointConfig endpoint, string queueName, QueueMessage message, CancellationToken ct = default);

    /// <summary>Creates (or replaces) a dynamic shovel on the broker identified by <paramref name="hostingEndpoint"/>.</summary>
    Task CreateShovelAsync(RabbitEndpointConfig hostingEndpoint, ShovelDefinition definition, CancellationToken ct = default);

    Task<ShovelStatus?> GetShovelStatusAsync(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName, CancellationToken ct = default);
}
