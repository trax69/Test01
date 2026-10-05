using RabbitMessageMigration.Core.Configuration;

namespace RabbitMessageMigration.Core.Tests.Configuration;

public class RabbitSettingsTests
{
    private static RabbitSettings CreateRabbitSettings(
        string host = "localhost",
        int port = 5672,
        string username = "guest",
        string password = "guest",
        string vhost = "/")
    {
        return new RabbitSettings(host, port, username, password, vhost);
    }

    // Cuando host es nulo entonces lanzar una excepción de tipo ArgumentNullException.
    [Fact]
    public void When_host_is_null_should_throw_argument_null_exception()
    {
        // Arrange
        string host = null!;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => CreateRabbitSettings(host: host));

        // Assert
        Assert.Equal("host", exception.ParamName);
    }


    // Cuando host es vacio "",en blanco " ", espacios, salto de linea, tabuladores entonces lanzar una excepción de tipo ArgumentException.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void When_host_is_empty_or_whitespace_should_throw_argument_exception(string host)
    {

        // Arrange InlineData

        // Act
        var exception = Assert.Throws<ArgumentException>(() => CreateRabbitSettings(host: host));
        // Assert
        Assert.Equal("host", exception.ParamName);
    }

    // Cuando el host es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.

    [Fact]
    public void When_host_is_valid_should_host_be_saved()
    {
        // Arrange
        var host = "localhost";

        // Act
        var rabbitSettings = CreateRabbitSettings(host: host);

        // Assert
        Assert.Equal(host, rabbitSettings.Host);
    }

    // Cuando el puerto sea <= 0 entonces lanzar una excepción de tipo ArgumentOutOfRangeException.
    // Cuando el puerto sea > 65535 entonces lanzar una excepción de tipo ArgumentOutOfRangeException.
    // Cuando el puerto es > 0 y <= 65535 entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
    // Cuando el usuario sea nulo entonces lanzar una excepción de tipo ArgumentNullException
    // Cuando el usuario sea "" entonces lanzar una excepción de tipo ArgumentException.
    // Cuando el usuario sea " " entonces lanzar una excepción de tipo ArgumentException.
    // Cuando el usuario es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
    // Cuando la contraseña sea nula entonces lanzar una excepción de tipo ArgumentNullException.
    // Cuando la contraseña sea "" entonces lanzar una excepción de tipo ArgumentException.
    // Cuando la contraseña sea " " entonces lanzar una excepción de tipo ArgumentException.
    // Cuando la contraseña es válida entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
    // Cuando Vhost sea " " entonces lanzar una excepción de tipo ArgumentException.
    // Cuando Vhost sea nulo o "" entonces el Vhost debe ser "/". 
    // Cuando Vhost es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
    // Cuando se convierte la configuración a texto entonces el texto no contiene la contraseña (usar una contraseña larga y reconocible).
    // Cuando se convierte la configuración a texto entonces el texto contiene el host.
}
