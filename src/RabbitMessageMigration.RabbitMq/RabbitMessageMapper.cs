using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

internal static class RabbitMessageMapper
{
    public static MessageProperties ToMessageProperties(IReadOnlyBasicProperties rabbitProperties)
    {
        return new MessageProperties
        {
            ContentType = rabbitProperties.ContentType,
            MessageId = rabbitProperties.MessageId
        };
    }
}