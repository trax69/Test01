namespace RabbitMessageMigration.Core.Messaging;
public interface IMessageDestination
{
    Task SendMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken);
}