using RabbitMessageMigration.Core.Messaging;

namespace RabbitMessageMigration.RabbitMq.Tests;

public class DeliveryTagRegistryTests
{
    // Cuando se registra un mensaje con su delivery tag, entonces al tomarlo se obtiene ese tag.
    [Fact]
    public void When_message_is_registered_should_return_its_delivery_tag_when_taken()
    {
        // Arrange
        var sut = new DeliveryTagRegistry();
        var message = new ReceivedMessage();
        sut.Register(message, 5);

        // Act
        var deliveryTag = sut.Take(message);

        // Assert
        Assert.Equal(5UL, deliveryTag);
    }

    // Cuando hay dos mensajes registrados, entonces cada uno devuelve su propio delivery tag.
    [Fact]
    public void When_two_messages_are_registered_should_return_the_tag_of_each_one()
    {
        // Arrange
        var sut = new DeliveryTagRegistry();
        var firstMessage = new ReceivedMessage();
        var secondMessage = new ReceivedMessage();
        sut.Register(firstMessage, 1);
        sut.Register(secondMessage, 2);

        // Act
        var secondTag = sut.Take(secondMessage);
        var firstTag = sut.Take(firstMessage);

        // Assert
        Assert.Equal(1UL, firstTag);
        Assert.Equal(2UL, secondTag);
    }

    // Cuando un mensaje ya se ha tomado, entonces no se puede tomar otra vez (confirmarlo dos veces cerraría el canal).
    [Fact]
    public void When_message_was_already_taken_should_throw_invalid_operation()
    {
        // Arrange
        var sut = new DeliveryTagRegistry();
        var message = new ReceivedMessage();
        sut.Register(message, 5);
        sut.Take(message);

        // Act y Assert
        Assert.Throws<InvalidOperationException>(() => sut.Take(message));
    }
    // Cuando el mensaje no se registró nunca, entonces tomarlo lanza InvalidOperationException.
    [Fact]
    public void When_message_was_never_registered_should_throw_invalid_operation()
    {
        // Arrange
        var sut = new DeliveryTagRegistry();

        // Act y Assert
        Assert.Throws<InvalidOperationException>(() => sut.Take(new ReceivedMessage()));
    }
}