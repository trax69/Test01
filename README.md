# RabbitShovelMigration

Migración de colas de un RabbitMQ a otro usando el plugin **Shovel**, sin perder los mensajes
que estén pendientes de entrega. El usuario introduce por consola la configuración del servidor
origen y del servidor destino.

## Qué hace exactamente

Para cada cola a migrar:

1. Lee la estructura de la cola en el origen (durabilidad, argumentos como `x-message-ttl`, etc.)
   vía la API HTTP de gestión de RabbitMQ y la clona en el destino si no existe ya.
2. Si la cola tiene mensajes pendientes de entrega, crea un Shovel dinámico de "un solo uso"
   (`src-delete-after: queue-length`): transfiere exactamente los mensajes que había en el
   momento de arrancar y luego se autodestruye. Si la cola está vacía, no hace falta Shovel.
3. Espera a que el Shovel termine y verifica que el número de mensajes migrados coincide con los
   que había en origen, generando un informe final por consola.

## Estructura del proyecto

```
src/RabbitShovelMigration.Core/      lógica de migración, sin dependencias externas
  RabbitEndpointConfig.cs            config de conexión (host de gestión vs. host AMQP interno)
  IRabbitManagementClient.cs         abstracción sobre la API HTTP de RabbitMQ
  RabbitManagementClient.cs          implementación real (HttpClient + System.Text.Json)
  ShovelMigrationService.cs          orquesta clonado de estructura + Shovel + verificación
  Models.cs                          records: QueueInfo, QueueMessage, ShovelDefinition, MigrationReport...
src/RabbitShovelMigration.Console/   app ejecutable, pide la config por consola
tests/RabbitShovelMigration.Tests/   xUnit, con un fake de IRabbitManagementClient (sin broker real)
docker/setup-rabbit-instances.ps1    levanta y configura rabbitA y rabbitB
definition_A.json                    definiciones de ejemplo (usuario, colas, exchanges, bindings)
```

## Requisitos de red en Docker

El Shovel lo ejecuta el broker origen, que necesita conectar **directamente** (AMQP, puerto 5672)
con el broker destino - no a través de `localhost` y los puertos publicados en el host de Windows,
sino dentro de la red Docker. Con `docker run` simple (red `bridge` por defecto) los contenedores
no se resuelven por nombre entre sí, así que `docker/setup-rabbit-instances.ps1` crea la red
`rabbit-migration-net`, conecta ambos contenedores a ella y habilita `rabbitmq_shovel` +
`rabbitmq_shovel_management`.

Por eso la app pide **dos hosts distintos** por servidor:
- **Host de gestión**: el que usa esta aplicación desde Windows para hablar con la API HTTP (`localhost`).
- **Host AMQP interno**: el que el *otro* broker usa para conectar por Shovel (`rabbitA` / `rabbitB`,
  el nombre de contenedor en la red compartida).

## Uso

```powershell
dotnet run --project src/RabbitShovelMigration.Console
```

1. Configuración del servidor **ORIGEN** (host de gestión, puerto de gestión, host AMQP interno,
   puerto AMQP, vhost, usuario, contraseña).
2. Lo mismo para el servidor **DESTINO**.
3. Qué colas migrar (nombres separados por comas, o vacío para migrar todas las del vhost).
4. Si los mensajes se mueven (por defecto) o se copian, dejándolos también en el origen.

Al final muestra una tabla con, por cada cola: mensajes antes, mensajes después, y el error si
algo falló.

## Datos de ejemplo

Con los contenedores levantados, importa las definiciones en `rabbitA` (crea colas, exchanges y bindings):

```powershell
curl -u root:root -H "Content-Type: application/json" -X POST -T ./definition_A.json http://localhost:9090/api/definitions
```

Las definiciones no incluyen mensajes: los mensajes pendientes se publican a mano desde el panel de gestión.

## Tests

```powershell
dotnet test
```