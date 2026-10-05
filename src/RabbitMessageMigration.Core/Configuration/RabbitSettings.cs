namespace RabbitMessageMigration.Core.Configuration;

public sealed class RabbitSettings
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    public RabbitSettings(string host, int port, string username, string password, string vhost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, MinPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, MaxPort);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        Username = username;
        Port = port;
        Host = host;
    }

    public string Host { get; }
    public int Port { get; }
    public string Username { get; }
}