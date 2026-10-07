namespace RabbitMessageMigration.Core.Messaging;

public interface IMessageSource
{
    Task<ReceivedMessage?> ReceiveMessageAsync(string queueName, CancellationToken cancellationToken);
}