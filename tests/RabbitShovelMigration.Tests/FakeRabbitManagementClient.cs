using RabbitShovelMigration.Core;

namespace RabbitShovelMigration.Tests;

/// <summary>
/// In-memory stand-in for the Management API. <see cref="CreateShovelAsync"/> moves nothing itself:
/// tests use <see cref="OnShovelCreated"/> to simulate the outcome (full or partial transfer, error, never finishes).
/// </summary>
public sealed class FakeRabbitManagementClient : IRabbitManagementClient
{
    private readonly Dictionary<QueueKey, QueueInfo> _queues = new();
    private readonly Dictionary<ShovelKey, ShovelStatus> _shovelStatuses = new();

    private readonly Dictionary<QueueKey, List<QueueMessage>> _messages = new();

    public List<(int ManagementPort, string Queue, QueueMessage Message)> PublishedMessages { get; } = new();
    public List<ShovelDefinition> CreatedShovels { get; } = new();

    public Action<ShovelDefinition>? OnShovelCreated { get; set; }

    public void SetQueue(RabbitEndpointConfig endpoint, QueueInfo queue) =>
        _queues[QueueKey.For(endpoint, queue.Name)] = queue;

    public void SetShovelStatus(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName, ShovelStatus? status)
    {
        var key = new ShovelKey(hostingEndpoint.ManagementHost, hostingEndpoint.ManagementPort, virtualHost, shovelName);
        if (status is null)
        {
            _shovelStatuses.Remove(key);
        }
        else
        {
            _shovelStatuses[key] = status;
        }
    }

    public Task<QueueInfo?> GetQueueAsync(RabbitEndpointConfig endpoint, string queueName, CancellationToken ct = default)
    {
        _queues.TryGetValue(QueueKey.For(endpoint, queueName), out var queue);
        return Task.FromResult(queue);
    }

    public Task<IReadOnlyList<string>> ListQueueNamesAsync(RabbitEndpointConfig endpoint, CancellationToken ct = default)
    {
        var names = _queues.Keys
            .Where(k => k.Host == endpoint.ManagementHost && k.Port == endpoint.ManagementPort && k.Vhost == endpoint.VirtualHost)
            .Select(k => k.Queue)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public Task DeclareQueueAsync(RabbitEndpointConfig endpoint, QueueInfo queue, CancellationToken ct = default)
    {
        _queues[QueueKey.For(endpoint, queue.Name)] = queue with { MessagesReady = 0 };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueueMessage>> PeekMessagesAsync(RabbitEndpointConfig endpoint, string queueName, long count, CancellationToken ct = default)
    {
        _messages.TryGetValue(QueueKey.For(endpoint, queueName), out var list);
        IReadOnlyList<QueueMessage> peeked = (list ?? new List<QueueMessage>()).Take((int)count).ToList();
        return Task.FromResult(peeked);
    }

    public Task<bool> PublishMessageAsync(RabbitEndpointConfig endpoint, string queueName, QueueMessage message, CancellationToken ct = default)
    {
        var key = QueueKey.For(endpoint, queueName);
        if (!_queues.TryGetValue(key, out var queue))
        {
            return Task.FromResult(false);
        }

        _queues[key] = queue with { MessagesReady = queue.MessagesReady + 1 };
        PublishedMessages.Add((endpoint.ManagementPort, queueName, message));
        return Task.FromResult(true);
    }

    public void SetMessages(RabbitEndpointConfig endpoint, string queueName, IEnumerable<QueueMessage> messages) =>
        _messages[QueueKey.For(endpoint, queueName)] = messages.ToList();

    public Task CreateShovelAsync(RabbitEndpointConfig hostingEndpoint, ShovelDefinition definition, CancellationToken ct = default)
    {
        CreatedShovels.Add(definition);
        OnShovelCreated?.Invoke(definition);
        return Task.CompletedTask;
    }

    public Task<ShovelStatus?> GetShovelStatusAsync(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName, CancellationToken ct = default)
    {
        _shovelStatuses.TryGetValue(new ShovelKey(hostingEndpoint.ManagementHost, hostingEndpoint.ManagementPort, virtualHost, shovelName), out var status);
        return Task.FromResult<ShovelStatus?>(status);
    }

    private readonly record struct QueueKey(string Host, int Port, string Vhost, string Queue)
    {
        public static QueueKey For(RabbitEndpointConfig endpoint, string queue) =>
            new(endpoint.ManagementHost, endpoint.ManagementPort, endpoint.VirtualHost, queue);
    }

    private readonly record struct ShovelKey(string Host, int Port, string Vhost, string Name);
}
