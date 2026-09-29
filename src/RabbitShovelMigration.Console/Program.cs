using RabbitShovelMigration.Core;

Console.WriteLine("=== Migración de colas RabbitMQ mediante Shovel ===");
Console.WriteLine("Introduce la configuración de los servidores de origen y destino.");
Console.WriteLine("Pulsa Enter para aceptar el valor por defecto mostrado entre corchetes.");

var source = ReadEndpoint(
    label: "ORIGEN",
    defaultManagementHost: "localhost", defaultManagementPort: 9090,
    defaultAmqpHost: "rabbitA", defaultAmqpPort: 5672,
    defaultVHost: "/", defaultUser: "root", defaultPass: "root");

var destination = ReadEndpoint(
    label: "DESTINO",
    defaultManagementHost: "localhost", defaultManagementPort: 9191,
    defaultAmqpHost: "rabbitB", defaultAmqpPort: 5672,
    defaultVHost: "/", defaultUser: "root", defaultPass: "root");

using var client = new RabbitManagementClient();

Console.WriteLine();
Console.Write("Colas a migrar (separadas por comas; vacío = todas las del vhost de origen): ");
var queuesInput = Console.ReadLine();

List<string> queues;
try
{
    queues = string.IsNullOrWhiteSpace(queuesInput)
        ? (await client.ListQueueNamesAsync(source)).ToList()
        : queuesInput.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
catch (Exception ex)
{
    Console.WriteLine($"No se pudo conectar con el servidor origen ({source.ManagementBaseUrl}): {ex.Message}");
    return 1;
}

if (queues.Count == 0)
{
    Console.WriteLine("No hay colas que migrar (el vhost de origen está vacío). Nada que hacer.");
    return 0;
}

Console.WriteLine();
Console.WriteLine("Por defecto los mensajes se MUEVEN (se eliminan del origen al pasar al destino).");
Console.WriteLine("Si eliges mantenerlos, se COPIAN: quedan en el origen y además se publican en el destino.");
var keepInSource = Confirm("¿Mantener los mensajes en el origen tras la migración?", defaultYes: false);

Console.WriteLine();
Console.WriteLine($"{(keepInSource ? "Copiando" : "Migrando")} {queues.Count} cola(s): {string.Join(", ", queues)}");
if (keepInSource)
{
    Console.WriteLine("Aviso: los mensajes copiados se marcan como 'redelivered' en el origen y, si ejecutas la copia dos veces, el destino tendrá duplicados.");
}

var service = new ShovelMigrationService(client);
var report = await service.MigrateAsync(source, destination, queues, keepInSource);

PrintReport(report);

return report.AllSucceeded ? 0 : 1;

static RabbitEndpointConfig ReadEndpoint(
    string label,
    string defaultManagementHost, int defaultManagementPort,
    string defaultAmqpHost, int defaultAmqpPort,
    string defaultVHost, string defaultUser, string defaultPass)
{
    Console.WriteLine();
    Console.WriteLine($"--- Servidor {label} ---");
    var managementHost = Prompt("Host de gestión (HTTP API, accesible desde esta máquina)", defaultManagementHost);
    var managementPort = PromptInt("Puerto de gestión (HTTP API)", defaultManagementPort);
    var amqpHost = Prompt("Host AMQP interno (el que el OTRO broker usa para conectar via Shovel, p.ej. nombre del contenedor Docker)", defaultAmqpHost);
    var amqpPort = PromptInt("Puerto AMQP interno", defaultAmqpPort);
    var vhost = Prompt("Virtual host", defaultVHost);
    var user = Prompt("Usuario", defaultUser);
    var pass = Prompt("Contraseña", defaultPass);

    return new RabbitEndpointConfig(managementHost, managementPort, amqpHost, amqpPort, vhost, user, pass);
}

static string Prompt(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var input = Console.ReadLine()?.Trim('﻿').Trim();
    return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
}

static int PromptInt(string label, int defaultValue)
{
    var raw = Prompt(label, defaultValue.ToString());
    return int.TryParse(raw, out var value) ? value : defaultValue;
}

static bool Confirm(string question, bool defaultYes)
{
    var hint = defaultYes ? "S/n" : "s/N";
    Console.Write($"{question} [{hint}]: ");
    var input = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(input))
    {
        return defaultYes;
    }
    return input is "s" or "si" or "sí" or "y" or "yes";
}

static void PrintReport(MigrationReport report)
{
    Console.WriteLine();
    Console.WriteLine("=== Info de migracion ===");
    Console.WriteLine($"{"Cola",-20} {"Estado",-10} {"Mensajes antes",-16} {"Mensajes después",-18} Detalle");
    Console.WriteLine(new string('-', 90));

    foreach (var result in report.Results)
    {
        var estado = result.Success ? "OK" : "ERROR";
        Console.WriteLine(
            $"{result.QueueName,-20} {estado,-10} {result.MessagesBeforeMigration,-16} {result.MessagesAfterMigration,-18} {result.ErrorMessage}");
    }

    Console.WriteLine(new string('-', 90));
    Console.WriteLine(report.AllSucceeded
        ? "Resultado: todas las colas se migraron correctamente."
        : "Resultado: alguna cola falló al migrar. Revisa el detalle anterior.");
}
