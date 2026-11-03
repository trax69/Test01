using RabbitMessageMigration.Core.Configuration;

namespace RabbitMessageMigration.Core.Tests.Configuration;

public class MigrationRequestTests
{
    private static RabbitSettings CreateRabbitSettings(
        string host = "localhost",
        int port = 5672,
        string username = "user",
        string password = "password",
        string vhost = "/")
    {
        return new RabbitSettings(host, port, username, password, vhost);
    }

    private static MigrationRequest CreateMigrationRequest(
        RabbitSettings? source = null,
        RabbitSettings? destination = null,
        IEnumerable<string>? queues = null)
    {
        return new MigrationRequest(
            source ?? CreateRabbitSettings(host: "source-host"),
            destination ?? CreateRabbitSettings(host: "destination-host"),
            queues ?? ["queue1"]);
    }

    // Si el origen es null entonces lanzar una excepción de tipo ArgumentNullException.

    // Si el destino es null entonces lanzar una excepción de tipo ArgumentNullException.

    // si la lista de colas es null entonces lanzar una excepción de tipo ArgumentNullException.

    // Si la lista de colas es vacía entonces lanzar una excepción de tipo ArgumentException.

    // Si algún nombre de cola es vacío o en blanco entonces lanzar una excepción de tipo ArgumentException.

    // si el origen y destino apuntan al mismo broker (mismo host, puerto y vhost) entonces lanzar una excepción de tipo ArgumentException.
    [Fact]
    public void When_source_and_destination_point_to_the_same_broker_should_throw_argument_exception()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "same-host", port: 5672, vhost: "/");
        var destination = CreateRabbitSettings(host: "same-host", port: 5672, vhost: "/");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => CreateMigrationRequest(source: source, destination: destination));

        // Assert
        Assert.Equal("destination", exception.ParamName);
    }

    // Si los datos son válidos entonces se construye correctamente la configuración de migración y queda guardado.
    [Fact]
    public void When_data_is_valid_should_source_be_saved()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");

        // Act
        var sut = CreateMigrationRequest(source: source);

        // Assert
        Assert.Same(source, sut.Source);
    }

    [Fact]
    public void When_data_is_valid_should_destination_be_saved()
    {
        // Arrange
        var destination = CreateRabbitSettings(host: "destination-host");

        // Act
        var sut = CreateMigrationRequest(destination: destination);

        // Assert
        Assert.Same(destination, sut.Destination);
    }

    [Fact]
    public void When_data_is_valid_should_queues_be_saved()
    {
        // Arrange
        var queues = new List<string> { "queue1", "queue2" };

        // Act
        var sut = CreateMigrationRequest(queues: queues);

        // Assert
        Assert.Equal(queues, sut.Queues);
    }
}