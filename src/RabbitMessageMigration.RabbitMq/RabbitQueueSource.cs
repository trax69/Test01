using RabbitMessageMigration.Core.Messaging;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

public sealed class RabbitQueueSource : IMessageSource, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly DeliveryTagRegistry _deliveryTags = new();
    private IChannel? _channel;
    private string? _channelQueue;
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

    private async Task<IChannel> GetChannelAsync(string queueName, CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } && _channelQueue == queueName)
        {
            return _channel;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channelQueue = queueName;
        return _channel;
    }

    public async Task<ReceivedMessage?> ReceiveMessageAsync(string queueName, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(queueName, cancellationToken);
        var result = await channel.BasicGetAsync(queueName, autoAck: false, cancellationToken);
        if (result is null)
        {
            return null;
        }

        var properties = RabbitMessageMapper.ToMessageProperties(result.BasicProperties);
        var message = new ReceivedMessage(result.Body.ToArray(), properties);
        _deliveryTags.Register(message, result.DeliveryTag);
        return message;
    }

    public async Task ConfirmMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken)
    {
        var deliveryTag = _deliveryTags.Take(message);
        await _channel!.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
    }

    public async Task RequeueMessageAsync(string queueName, ReceivedMessage message, CancellationToken cancellationToken)
    {
        var deliveryTag = _deliveryTags.Take(message);
        await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
            _channelQueue = null;
        }
    }
}