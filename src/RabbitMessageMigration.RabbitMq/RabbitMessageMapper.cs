using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

internal static class RabbitMessageMapper
{
    public static MessageProperties ToMessageProperties(IReadOnlyBasicProperties rabbitProperties)
    {
        return new MessageProperties(ToCoreHeaders(rabbitProperties.Headers))
        {
            ContentType = rabbitProperties.ContentType,
            MessageId = rabbitProperties.MessageId,
            Persistent = rabbitProperties.DeliveryMode == DeliveryModes.Persistent
        };
    }

    private static Dictionary<string, object> ToCoreHeaders(IDictionary<string, object?>? rabbitHeaders)
    {
        var headers = new Dictionary<string, object>();
        if (rabbitHeaders is null)
        {
            return headers;
        }

        foreach (var (key, value) in rabbitHeaders)
        {
            if (value is not null)
            {
                headers[key] = value;
            }
        }
        return headers;
    }
}