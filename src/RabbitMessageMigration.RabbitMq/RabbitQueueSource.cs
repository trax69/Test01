using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

public sealed class RabbitQueueSource : IMessageSource
{
    private readonly IConnection _connection;

    public RabbitQueueSource(IConnection connection)
    {
        _connection = connection;
    }

    public async Task<int> GetMessageCountAsync(string queueName, CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queue = await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
        return (int)queue.MessageCount;
    }

    public async Task<ReceivedMessage?> ReceiveMessageAsync(string queueName, CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var result = await channel.BasicGetAsync(queueName, autoAck: false, cancellationToken);
        if (result is null)
        {
            return null;
        }

        throw new NotImplementedException();
    }

    public Task ConfirmMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task RequeueMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}