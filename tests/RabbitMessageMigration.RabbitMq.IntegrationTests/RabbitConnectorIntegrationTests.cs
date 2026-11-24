using RabbitMessageMigration.Core.Configuration;
using RabbitMQ.Client.Exceptions;

namespace RabbitMessageMigration.RabbitMq.IntegrationTests;

public class RabbitConnectorIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _rabbit;

    public RabbitConnectorIntegrationTests(RabbitMqFixture rabbit)
    {
        _rabbit = rabbit;
    }

    // Cuando el broker es alcanzable y las credenciales son correctas, entonces se abre la conexión.
    [Fact]
    public async Task When_broker_is_reachable_should_open_a_connection()
    {
        // Act
        await using var connection = await RabbitConnector.ConnectAsync(_rabbit.Settings);

        // Assert
        Assert.True(connection.IsOpen);
    }

    // Cuando la contraseña es incorrecta, entonces falla con BrokerUnreachableException.
    [Fact]
    public async Task When_password_is_wrong_should_throw_broker_unreachable()
    {
        // Arrange
        var wrongSettings = new RabbitSettings(
            _rabbit.Settings.Host,
            _rabbit.Settings.Port,
            _rabbit.Settings.Username,
            "contraseña-incorrecta",
            _rabbit.Settings.Vhost);

        // Act & Assert
        await Assert.ThrowsAsync<BrokerUnreachableException>(() => RabbitConnector.ConnectAsync(wrongSettings));
    }
}