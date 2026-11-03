namespace RabbitMessageMigration.Core.Configuration;

public sealed class MigrationRequest
{
    public MigrationRequest(RabbitSettings source, RabbitSettings destination, IEnumerable<string> queues)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(queues);

        var queueList = queues.ToList();
        if (!queueList.Any())
        {
            throw new ArgumentException("La lista de colas no puede estar vacía.", nameof(queues));
        }

        if (queueList.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Los nombres de las colas no pueden estar vacíos o en blanco.", nameof(queues));
        }

        if (PointToSameBroker(source, destination))
        {
            throw new ArgumentException("El origen y el destino no pueden apuntar al mismo broker.", nameof(destination));
        }

        Source = source;
        Destination = destination;
        Queues = queueList;
    }

    private static bool PointToSameBroker(RabbitSettings source, RabbitSettings destination)
    {
        return string.Equals(source.Host, destination.Host, StringComparison.OrdinalIgnoreCase)
            && source.Port == destination.Port
            && source.Vhost == destination.Vhost;
    }

    public RabbitSettings Source { get; }
    public RabbitSettings Destination { get; }
    public IReadOnlyList<string> Queues { get; }
}