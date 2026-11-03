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

    // si messageId está presente entonces se debe copiar a MessageProperties.MessageId.

    // si entrega persistente está presente entonces se debe establecer MessageProperties.Persistent en true.

    // si entrega transitoria o no está presente entonces se debe establecer MessageProperties.Persistent en false.

    // si headers están presentes entonces se deben copiar a MessageProperties.Headers.
}
