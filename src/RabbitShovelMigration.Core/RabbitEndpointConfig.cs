namespace RabbitShovelMigration.Core;

/// <summary>
/// Connection details for one broker. <see cref="ManagementHost"/> is how this application reaches the
/// HTTP API (e.g. "localhost" via a Docker port mapping); <see cref="AmqpHost"/> is how the OTHER broker
/// reaches this one over AMQP for the Shovel (e.g. the container name on a shared Docker network).
/// </summary>
public sealed record RabbitEndpointConfig(
    string ManagementHost,
    int ManagementPort,
    string AmqpHost,
    int AmqpPort,
    string VirtualHost,
    string Username,
    string Password)
{
    public string ManagementBaseUrl => $"http://{ManagementHost}:{ManagementPort}";

    /// <summary>The default vhost "/" must be encoded as "%2F": an empty path segment means the empty-named vhost (rabbitmq.com/docs/uri-spec).</summary>
    public string ToAmqpUri()
    {
        var vhostSegment = Uri.EscapeDataString(VirtualHost);
        return $"amqp://{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}@{AmqpHost}:{AmqpPort}/{vhostSegment}";
    }
}
