namespace RabbitMessageMigration.Core.Messaging;

public sealed class ReceivedMessage
{
    public ReceivedMessage()
        : this([])
    {
    }

    public ReceivedMessage(byte[] body)
        : this(body, new MessageProperties())
    {
    }

    public ReceivedMessage(byte[] body, MessageProperties properties)
        : this(body, properties, string.Empty)
    {
    }

    public ReceivedMessage(byte[] body, MessageProperties properties, string routingKey)
    {
        Body = body;
        Properties = properties;
        RoutingKey = routingKey;
    }

    public byte[] Body { get; }

    public MessageProperties Properties { get; }

    public string RoutingKey { get; }
}