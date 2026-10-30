using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.Core.Tests.Messaging;

public class ReceivedMessageTests
{
    [Fact]
    public void When_message_is_created_should_expose_its_body()
    {
        var body = new byte[] { 1, 2, 3 };

        var sut = new ReceivedMessage(body);

        Assert.Same(body, sut.Body);
    }
}
