namespace RabbitMessageMigration.Core.Configuration;

public sealed class RabbitSettings
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const string DefaultVhost = "/";

    public RabbitSettings(string host, int port, string username, string password, string vhost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, MinPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, MaxPort);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (string.IsNullOrEmpty(vhost))
        {
            vhost = DefaultVhost;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(vhost);

        Username = username;
        Password = password;
        Port = port;
        Host = host;
        Vhost = vhost;
    }

    public string Username { get; }
    public string Password { get; }
    public int Port { get; }
    public string Host { get; }
    public string Vhost { get; }
}