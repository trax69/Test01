using RabbitShovelMigration.Core;

namespace RabbitShovelMigration.Tests;

public class RabbitEndpointConfigTests
{
    [Fact]
    public void ToAmqpUri_DefaultVirtualHost_IsEncodedAsPercent2F()
    {
        var endpoint = new RabbitEndpointConfig("localhost", 9090, "rabbitA", 5672, "/", "root", "root");

        Assert.Equal("amqp://root:root@rabbitA:5672/%2F", endpoint.ToAmqpUri());
    }

    [Fact]
    public void ToAmqpUri_CustomVirtualHost_IsEscapedAndAppended()
    {
        var endpoint = new RabbitEndpointConfig("localhost", 9090, "rabbitA", 5672, "orders/eu", "root", "root");

        Assert.Equal("amqp://root:root@rabbitA:5672/orders%2Feu", endpoint.ToAmqpUri());
    }

    [Fact]
    public void ManagementBaseUrl_UsesManagementHostAndPort_NotAmqpHost()
    {
        var endpoint = new RabbitEndpointConfig("localhost", 9090, "rabbitA", 5672, "/", "root", "root");

        Assert.Equal("http://localhost:9090", endpoint.ManagementBaseUrl);
    }
}
