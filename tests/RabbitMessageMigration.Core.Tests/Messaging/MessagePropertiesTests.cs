using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.Core.Tests.Messaging;

public class MessagePropertiesTests
{
    [Fact]
    public void When_properties_are_created_without_headers_should_expose_empty_headers()
    {
        // Arrange / Act
        var sut = new MessageProperties();

        // Assert
        Assert.Empty(sut.Headers);
    }

    [Fact]
    public void When_properties_are_created_without_content_type_should_expose_null_content_type()
    {
        // Arrange / Act
        var sut = new MessageProperties();

        // Assert
        Assert.Null(sut.ContentType);
    }

    [Fact]
    public void When_properties_are_created_with_headers_should_expose_its_headers()
    {
        // Arrange
        var headers = new Dictionary<string, object>
        {
            { "header1", "value1" }
        };

        // Act
        var sut = new MessageProperties(headers);

        // Assert
        Assert.Equal(headers, sut.Headers);
    }

    [Fact]
    public void When_properties_are_created_with_content_type_should_expose_its_content_type()
    {
        // Arrange
        var contentType = "application/json";

        // Act
        var sut = new MessageProperties
        {
            ContentType = contentType
        };

        // Assert
        Assert.Equal(contentType, sut.ContentType);
    }

    [Fact]
    public void When_properties_are_created_without_message_id_should_expose_null_message_id()
    {
        // Arrange / Act
        var sut = new MessageProperties();

        // Assert
        Assert.Null(sut.MessageId);
    }

}