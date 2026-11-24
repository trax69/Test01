using RabbitMQ.Client;

namespace RabbitMessageMigration.RabbitMq.Tests;

public class RabbitMessageMapperTests
{
    // Si las propiedades de RabbitMQ no tienen content type entonces el content type es null.
    [Fact]
    public void When_rabbit_properties_have_no_content_type_should_map_null_content_type()
    {
        // Arrange
        var rabbitProperties = new BasicProperties();

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Null(result.ContentType);
    }

    // si contentType está presente entonces se debe copiar a MessageProperties.ContentType.
    [Fact]
    public void When_content_type_is_present_should_map_content_type()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            ContentType = "application/json"
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Equal("application/json", result.ContentType);
    }

    // si messageId está presente entonces se debe copiar a MessageProperties.MessageId.
    [Fact]
    public void When_message_id_is_present_should_map_message_id()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            MessageId = "12345"
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Equal("12345", result.MessageId);
    }

    // si entrega persistente está presente entonces se debe establecer MessageProperties.Persistent en true.
    [Fact]
    public void When_delivery_mode_is_persistent_should_set_persistent_true()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.True(result.Persistent);
    }

    // si entrega transitoria está presente entonces se debe establecer MessageProperties.Persistent en false.
    [Fact]
    public void When_delivery_mode_is_transient_should_set_persistent_false()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Transient
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.False(result.Persistent);
    }

    // si deliveryMode no está presente entonces se debe establecer MessageProperties.Persistent en false.
    [Fact]
    public void When_delivery_mode_is_not_present_should_set_persistent_false()
    {
        // Arrange
        var rabbitProperties = new BasicProperties();

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.False(result.Persistent);
    }

    // si headers es null o no está presente entonces se debe copiar a MessageProperties.Headers como un diccionario vacío.
    [Fact]
    public void When_headers_are_null_or_not_present_should_map_empty_headers()
    {
        // Arrange
        var rabbitProperties = new BasicProperties();

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Empty(result.Headers);
    }

    // si headers está presente entonces se deben copiar a MessageProperties.Headers.
    [Fact]
    public void When_headers_are_present_should_copy_them_to_message_properties()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-tenant"] = "acme",
                ["x-retries"] = 3
            }
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Equal(2, result.Headers.Count);
        Assert.Equal("acme", result.Headers["x-tenant"]);
        Assert.Equal(3, result.Headers["x-retries"]);
    }

    // si un valor de header es null entonces ese header no se copia.
    [Fact]
    public void When_a_header_value_is_null_should_not_copy_that_header()
    {
        // Arrange
        var rabbitProperties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["x-tenant"] = "acme",
                ["x-empty"] = null
            }
        };

        // Act
        var result = RabbitMessageMapper.ToMessageProperties(rabbitProperties);

        // Assert
        Assert.Equal(["x-tenant"], result.Headers.Keys);
    }
}
