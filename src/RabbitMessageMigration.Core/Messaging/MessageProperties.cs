namespace RabbitMessageMigration.Core.Messaging;

public sealed class MessageProperties
{
    public MessageProperties(IDictionary<string, object> headers)
    {
        Headers = headers;
    }

    public MessageProperties()
        : this(new Dictionary<string, object>())
    {
    }

    public IDictionary<string, object> Headers { get; }
    public string? ContentType { get; init; }
    public string? MessageId { get; init; }
}