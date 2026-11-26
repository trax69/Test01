using RabbitMessageMigration.Console.Output;
using RabbitMessageMigration.Core.Reporting;

namespace RabbitMessageMigration.Console.Tests.Output;

public class ConsolePrinterTests
{
    // Cuando una cola se migra bien, entonces se muestra su nombre y los mensajes migrados.
    [Fact]
    public void When_queue_succeeds_should_print_name_and_migrated_count()
    {
        // Arrange
        var report = new MigrationReport([new QueueMigrationEntry("pedidos", new QueueMigrationResult(3))]);
        var output = new StringWriter();
        var printer = new ConsolePrinter(output);

        // Act
        printer.Print(report);

        // Assert
        var text = output.ToString();
        Assert.Contains("pedidos", text);
        Assert.Contains("3", text);
    }
}