namespace RabbitMessageMigration.Core.Messaging;

public interface IMessageSource
{
    Task<ReceivedMessage?> ReceiveMessageAsync(string queueName, CancellationToken cancellationToken);

    Task ConfirmMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken);

    Task RequeueMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken);

    Task<int> GetMessageCountAsync(
        string queueName,
        CancellationToken cancellationToken);

}
