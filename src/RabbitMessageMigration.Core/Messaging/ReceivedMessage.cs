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
    {
        Body = body;
        Properties = properties;
    }

    public byte[] Body { get; }

    public MessageProperties Properties { get; }
}