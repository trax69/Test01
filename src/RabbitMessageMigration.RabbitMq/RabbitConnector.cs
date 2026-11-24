using RabbitMessageMigration.Core.Configuration;
using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq;

public static class RabbitConnector
{
    public static async Task<IConnection> ConnectAsync(RabbitSettings settings, CancellationToken cancellationToken = default)
    {
        return await CreateFactory(settings).CreateConnectionAsync(cancellationToken);
    }
    
    internal static ConnectionFactory CreateFactory(RabbitSettings settings)
    {
        return new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.Vhost,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false
        };
    }
}