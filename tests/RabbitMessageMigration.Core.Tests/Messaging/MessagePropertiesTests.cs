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
    public void When_properties_are_created_should_expose_its_headers()
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
}