namespace RabbitShovelMigration.Core;

/// <summary>
/// Migrates queues between brokers: clones the queue structure, moves pending messages with a one-shot Shovel
/// and verifies the destination count. A Shovel always consumes from its source, so when messages must be KEPT
/// in the source they are peeked through the Management API and republished on the destination instead.
/// </summary>
public sealed class ShovelMigrationService
{
    private readonly IRabbitManagementClient _client;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ShovelMigrationService(
        IRabbitManagementClient client,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _client = client;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _delay = delay ?? Task.Delay;
    }

    public async Task<MigrationReport> MigrateAsync(
        RabbitEndpointConfig source,
        RabbitEndpointConfig destination,
        IEnumerable<string> queueNames,
        bool keepMessagesInSource = false,
        CancellationToken ct = default)
    {
        var results = new List<QueueMigrationResult>();
        foreach (var queueName in queueNames)
        {
            results.Add(await MigrateQueueAsync(source, destination, queueName, keepMessagesInSource, ct));
        }
        return new MigrationReport(results);
    }

    public async Task<QueueMigrationResult> MigrateQueueAsync(
        RabbitEndpointConfig source,
        RabbitEndpointConfig destination,
        string queueName,
        bool keepMessagesInSource = false,
        CancellationToken ct = default)
    {
        long messagesBefore = 0;
        try
        {
            var sourceQueue = await _client.GetQueueAsync(source, queueName, ct);
            if (sourceQueue is null)
            {
                return QueueMigrationResult.Failed(queueName, 0, "La cola no existe en el servidor origen.");
            }

            messagesBefore = sourceQueue.MessagesReady;

            if (await _client.GetQueueAsync(destination, queueName, ct) is null)
            {
                await _client.DeclareQueueAsync(destination, sourceQueue, ct);
            }

            if (messagesBefore == 0)
            {
                return new QueueMigrationResult(queueName, Success: true, messagesBefore, 0, ErrorMessage: null);
            }

            if (keepMessagesInSource)
            {
                return await CopyMessagesAsync(source, destination, queueName, messagesBefore, ct);
            }

            var shovelName = $"migrate-{queueName}";
            var definition = new ShovelDefinition(
                Name: shovelName,
                VirtualHost: source.VirtualHost,
                SourceUri: source.ToAmqpUri(),
                SourceQueue: queueName,
                DestinationUri: destination.ToAmqpUri(),
                DestinationQueue: queueName);

            await _client.CreateShovelAsync(source, definition, ct);

            var terminationError = await WaitForShovelToFinishAsync(source, shovelName, ct);
            if (terminationError is not null)
            {
                return QueueMigrationResult.Failed(queueName, messagesBefore, terminationError);
            }

            var messagesAfter = await WaitForDestinationMessagesAsync(destination, queueName, messagesBefore, ct);

            if (messagesAfter < messagesBefore)
            {
                return QueueMigrationResult.Failed(
                    queueName,
                    messagesBefore,
                    $"Solo se migraron {messagesAfter} de {messagesBefore} mensajes.");
            }

            return new QueueMigrationResult(queueName, Success: true, messagesBefore, messagesAfter, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return QueueMigrationResult.Failed(queueName, messagesBefore, ex.Message);
        }
    }

    /// <summary>Copies pending messages without consuming them; the expected count is relative to what the destination already holds.</summary>
    private async Task<QueueMigrationResult> CopyMessagesAsync(
        RabbitEndpointConfig source,
        RabbitEndpointConfig destination,
        string queueName,
        long messagesBefore,
        CancellationToken ct)
    {
        var destinationBefore = (await _client.GetQueueAsync(destination, queueName, ct))?.MessagesReady ?? 0;

        var messages = await _client.PeekMessagesAsync(source, queueName, messagesBefore, ct);
        if (messages.Count < messagesBefore)
        {
            return QueueMigrationResult.Failed(
                queueName,
                messagesBefore,
                $"Solo se pudieron leer {messages.Count} de {messagesBefore} mensajes del origen (¿hay consumidores activos?). No se ha copiado nada.");
        }

        var published = 0;
        foreach (var message in messages)
        {
            if (!await _client.PublishMessageAsync(destination, queueName, message, ct))
            {
                return QueueMigrationResult.Failed(
                    queueName,
                    messagesBefore,
                    $"El destino no pudo enrutar un mensaje a la cola (copiados {published} de {messagesBefore}).");
            }
            published++;
        }

        var expected = destinationBefore + published;
        var messagesAfter = await WaitForDestinationMessagesAsync(destination, queueName, expected, ct);
        if (messagesAfter < expected)
        {
            return QueueMigrationResult.Failed(
                queueName,
                messagesBefore,
                $"Se publicaron {published} mensajes pero el destino solo muestra {messagesAfter} (esperados {expected}).");
        }

        return new QueueMigrationResult(queueName, Success: true, messagesBefore, messagesAfter, ErrorMessage: null);
    }

    /// <summary>Polls until the shovel terminates. Returns an error message on failure/timeout, null on clean success.</summary>
    private async Task<string?> WaitForShovelToFinishAsync(RabbitEndpointConfig hostingEndpoint, string shovelName, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + _timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await _client.GetShovelStatusAsync(hostingEndpoint, hostingEndpoint.VirtualHost, shovelName, ct);

            if (status is null)
            {
                // A finished queue-length shovel removes itself: gone means done.
                return null;
            }

            if (status.State == ShovelState.Terminated)
            {
                return string.IsNullOrEmpty(status.Reason) || status.Reason == "Shovel finished"
                    ? null
                    : status.Reason;
            }

            await _delay(_pollInterval, ct);
        }

        return $"Tiempo de espera agotado esperando a que el shovel '{shovelName}' finalizase.";
    }

    /// <summary>Management stats (messages_ready) refresh periodically, so a single read right after the shovel ends can be stale; poll instead.</summary>
    private async Task<long> WaitForDestinationMessagesAsync(RabbitEndpointConfig destination, string queueName, long expectedAtLeast, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + _timeout;
        long lastSeen = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var queue = await _client.GetQueueAsync(destination, queueName, ct);
            lastSeen = queue?.MessagesReady ?? 0;
            if (lastSeen >= expectedAtLeast)
            {
                return lastSeen;
            }
            await _delay(_pollInterval, ct);
        }

        return lastSeen;
    }
}
