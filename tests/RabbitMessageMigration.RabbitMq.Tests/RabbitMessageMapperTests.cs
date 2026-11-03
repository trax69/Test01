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

    // si headers están presentes entonces se deben copiar a MessageProperties.Headers.
}
