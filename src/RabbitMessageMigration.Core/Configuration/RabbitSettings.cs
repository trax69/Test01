namespace RabbitMessageMigration.Core.Configuration;

public sealed class RabbitSettings
{
    public RabbitSettings(string host, int port, string username, string password, string vhost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        Host = host;
    }

    public string Host { get; }
}