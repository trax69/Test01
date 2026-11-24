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
        if (!_deliveryTags.Remove(message, out var deliveryTag))
        {
            throw new InvalidOperationException(
                "El mensaje no está registrado: no lo entregó este origen o ya se confirmó o se devolvió.");
        }

        return deliveryTag;
    }
}