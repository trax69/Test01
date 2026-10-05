namespace RabbitMessageMigration.Core.Configuration;

public sealed class RabbitSettings
{
    public RabbitSettings(string host, int port, string username, string password, string vhost)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host), "Host cannot be null.");
        }

        if (host == string.Empty)
        {
            throw new ArgumentException("Host cannot be empty.", nameof(host));
        }

        if (host == " ")
        {
            throw new ArgumentException("Host cannot be whitespace.", nameof(host));
        }

        Host = host;
    }

    public string Host { get; }
}