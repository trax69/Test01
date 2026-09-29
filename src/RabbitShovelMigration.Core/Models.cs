using System.Text.Json;

namespace RabbitShovelMigration.Core;

public sealed record QueueInfo(
    string Name,
    bool Durable,
    bool AutoDelete,
    IReadOnlyDictionary<string, object?> Arguments,
    long MessagesReady);

/// <summary><see cref="Payload"/> keeps the encoding it was read with (base64 for peeked messages, so binary bodies survive the round trip).</summary>
public sealed record QueueMessage(string Payload, string PayloadEncoding, JsonElement Properties);

public sealed record ShovelDefinition(
    string Name,
    string VirtualHost,
    string SourceUri,
    string SourceQueue,
    string DestinationUri,
    string DestinationQueue)
{
    /// <summary>"queue-length" makes the shovel one-shot: it moves only the messages already queued when it started, then removes itself.</summary>
    public string SourceDeleteAfter { get; init; } = "queue-length";

    public string AckMode { get; init; } = "on-confirm";
}

public enum ShovelState
{
    Unknown,
    Starting,
    Running,
    Terminated,
}

public sealed record ShovelStatus(string Name, ShovelState State, string? Reason);

public sealed record QueueMigrationResult(
    string QueueName,
    bool Success,
    long MessagesBeforeMigration,
    long MessagesAfterMigration,
    string? ErrorMessage)
{
    public static QueueMigrationResult Failed(string queueName, long messagesBefore, string errorMessage) =>
        new(queueName, Success: false, messagesBefore, MessagesAfterMigration: 0, errorMessage);
}

public sealed record MigrationReport(IReadOnlyList<QueueMigrationResult> Results)
{
    public bool AllSucceeded => Results.Count > 0 && Results.All(r => r.Success);
}
