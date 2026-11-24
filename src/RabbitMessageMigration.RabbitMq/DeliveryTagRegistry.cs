using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.RabbitMq;

internal sealed class DeliveryTagRegistry
{
    // Por referencia: dos mensajes con el mismo contenido son mensajes distintos.
    private readonly Dictionary<ReceivedMessage, ulong> _deliveryTags = new(ReferenceEqualityComparer.Instance);

    public void Register(ReceivedMessage message, ulong deliveryTag)
    {
        _deliveryTags[message] = deliveryTag;
    }

    public ulong Take(ReceivedMessage message)
    {
        return _deliveryTags[message];
    }
}