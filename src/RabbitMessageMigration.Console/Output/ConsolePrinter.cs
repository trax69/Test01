using RabbitMessageMigration.Core.Reporting;

namespace RabbitMessageMigration.Console.Output;

public sealed class ConsolePrinter
{
    private readonly TextWriter _output;

    public ConsolePrinter(TextWriter output)
    {
        _output = output;
    }

    public void Print(MigrationReport report)
    {
        foreach (var entry in report.Results)
        {
            _output.WriteLine($"{entry.QueueName}: {entry.Result.MigratedMessages} mensajes migrados");
        }
    }
}