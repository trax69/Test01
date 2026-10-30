namespace RabbitMessageMigration.Core.Messaging;

public sealed class ReceivedMessage
{
    public ReceivedMessage()
        : this([])
    {
    }

    public ReceivedMessage(byte[] body)
    {
        Body = body;
    }

    public byte[] Body { get; }
}