using RabbitMessageMigration.Core.Configuration;

namespace RabbitMessageMigration.RabbitMq.Tests;

public class RabbitConnectorTests
{
    // Cuando se crea la fábrica de conexiones, entonces usa host, puerto, usuario, contraseña y vhost de la configuración.
    [Fact]
    public void When_factory_is_created_should_use_the_rabbit_settings()
    {
        // Arrange
        var settings = new RabbitSettings("broker.example.com", 5673, "user", "secret", "/prod");

        // Act
        var factory = RabbitConnector.CreateFactory(settings);

        // Assert
        Assert.Equal("broker.example.com", factory.HostName);
        Assert.Equal(5673, factory.Port);
        Assert.Equal("user", factory.UserName);
        Assert.Equal("secret", factory.Password);
        Assert.Equal("/prod", factory.VirtualHost);
    }

    // Cuando se crea la fábrica de conexiones, entonces la reconexión automática está desactivada.
    [Fact]
    public void When_factory_is_created_should_disable_automatic_recovery()
    {
        // Arrange
        var settings = new RabbitSettings("broker.example.com", 5672, "user", "secret", "/");

        // Act
        var factory = RabbitConnector.CreateFactory(settings);

        // Assert
        Assert.False(factory.AutomaticRecoveryEnabled);
        Assert.False(factory.TopologyRecoveryEnabled);
    }
}