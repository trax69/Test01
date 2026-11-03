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
    [Fact]
    public void When_source_is_null_should_throw_argument_null_exception()
    {
        // Arrange
        var destination = CreateRabbitSettings(host: "destination-host");
        var queues = new List<string> { "queue1" };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new MigrationRequest(null!, destination, queues));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    // Si el destino es null entonces lanzar una excepción de tipo ArgumentNullException.
    [Fact]
    public void When_destination_is_null_should_throw_argument_null_exception()
    {
        // Arrange
        var queues = new List<string> { "queue2" };
        var source = CreateRabbitSettings(host: "source-host");

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new MigrationRequest(source, null!, queues));

        // Assert
        Assert.Equal("destination", exception.ParamName);
    }

    // si la lista de colas es null entonces lanzar una excepción de tipo ArgumentNullException.
    [Fact]
    public void When_queues_is_null_should_throw_argument_null_exception()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "destination-host");

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new MigrationRequest(source, destination, null!));

        // Assert
        Assert.Equal("queues", exception.ParamName);
    }

    // Si la lista de colas es vacía entonces lanzar una excepción de tipo ArgumentException.
    [Fact]
    public void When_queues_is_empty_should_throw_argument_exception()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "destination-host");
        var queues = new List<string>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new MigrationRequest(source, destination, queues));

        // Assert
        Assert.Equal("queues", exception.ParamName);
    }

    // Si algún nombre de cola es vacío o en blanco entonces lanzar una excepción de tipo ArgumentException.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void When_any_queue_name_is_empty_or_whitespace_should_throw_argument_exception(string queueName)
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "destination-host");
        var queues = new List<string> { "queue1", queueName };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new MigrationRequest(source, destination, queues));

        // Assert
        Assert.Equal("queues", exception.ParamName);
    }

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

    [Fact]
    public void When_source_and_destination_hosts_differ_only_in_casing_should_throw_argument_exception()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "SOURCE-HOST");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => CreateMigrationRequest(source: source, destination: destination));

        // Assert
        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void When_vhosts_differ_only_in_casing_should_not_throw()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "host", vhost: "/prod");
        var destination = CreateRabbitSettings(host: "host", vhost: "/PROD");

        // Act
        var exception = Record.Exception(() => CreateMigrationRequest(source: source, destination: destination));

        // Assert
        Assert.Null(exception);
    }

    // Si la lista de colas contiene un nombre repetido entonces lanzar una excepción de tipo ArgumentException.
    [Fact]
    public void When_queues_contains_duplicate_names_should_throw_argument_exception()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "destination-host");
        var queues = new List<string> { "queue1", "queue2", "queue1" };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => new MigrationRequest(source, destination, queues));

        // Assert
        Assert.Equal("queues", exception.ParamName);
    }

    // Si dos nombres de cola solo se diferencian en mayúsculas entonces no lanzar excepción.
    [Fact]
    public void When_queues_contains_names_that_differ_only_in_casing_should_not_throw()
    {
        // Arrange
        var source = CreateRabbitSettings(host: "source-host");
        var destination = CreateRabbitSettings(host: "destination-host");
        var queues = new List<string> { "queue1", "QUEUE1" };

        // Act
        var exception = Record.Exception(() => new MigrationRequest(source, destination, queues));

        // Assert
        Assert.Null(exception);
    }

}