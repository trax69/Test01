## Decisiones de diseño

### Vhost de `RabbitSettings`

- **Decisión:** si el `Vhost` es `null` o `""`, se entiende que quien usa la herramienta quiere el vhost por defecto y se usa `"/"`. Si el `Vhost` contiene solo espacios (`" "`), se lanza una `ArgumentException`.
- **Motivo:** dejar el vhost vacío significa "no he indicado ninguno", mientras que escribir solo espacios u otras teclas es, con mucha probabilidad, un error al teclear.
- **Alternativa descartada:** tratar `" "` igual que `""` y usar `"/"`. Ocultaría el error de tecleo y la herramienta, que mueve mensajes, podría moverlos desde o hacia el vhost `"/"` sin que quien la usa lo hubiera pedido.

### Contraseña de `RabbitSettings`

- **Decisión:** al convertir la configuración a texto (`ToString`, interpolación de strings, `Console.WriteLine`), la contraseña no se muestra. La propiedad sigue guardando el valor real, porque RabbitMQ lo necesita para conectar.
- **Motivo:** evitar que la contraseña acabe en pantalla o en un log sin que nadie lo haya querido.
- **Límite conocido:** no protege si alguien lee la propiedad de la contraseña y la imprime directamente.

### Origen y destino de `MigrationRequest`

- **Decisión:** el origen y el destino se consideran el mismo broker cuando coinciden el host, el puerto y el vhost, y en ese caso se lanza una `ArgumentException`. El host se compara sin distinguir mayúsculas; el vhost sí las distingue. El usuario y la contraseña no cuentan.
- **Motivo:**
  - Mover mensajes de una cola a sí misma la vaciaría y la volvería a llenar, con riesgo de perder o duplicar mensajes. Es un error grave y por eso se rechaza antes de empezar.
  - Los nombres de host no distinguen mayúsculas (`LOCALHOST` y `localhost` son la misma máquina), así que compararlos distinguiéndolas dejaría pasar justo el caso peligroso.
  - Los vhost de RabbitMQ sí distinguen mayúsculas (`/Prod` y `/prod` son dos vhosts distintos), así que ignorarlas bloquearía una migración legítima.
  - Las credenciales no cambian el destino real de los mensajes: dos usuarios distintos sobre el mismo host, puerto y vhost siguen apuntando al mismo broker.
- **Alternativa descartada:** comparar los dos `RabbitSettings` con `Equals`. Compara referencias, así que solo detectaría el caso en que se pasa el mismo objeto dos veces.
- **Límite conocido:** no se resuelven alias. `localhost` y `127.0.0.1`, o dos nombres DNS de la misma máquina, se consideran brokers distintos.

### Lista de colas de `MigrationRequest`

- **Decisión:** la lista de colas no puede ser `null` (`ArgumentNullException`), ni estar vacía, ni contener nombres vacíos, en blanco o repetidos (`ArgumentException`). Los nombres se comparan distinguiendo mayúsculas: `queue1` y `QUEUE1` son dos colas distintas. La petición guarda una copia de la lista.
- **Motivo:**
  - Un nombre repetido o en blanco es, con mucha probabilidad, un error al teclear. Rechazarlo avisa al usuario en lugar de ocultarlo.
  - Si se deduplicara en silencio, el usuario no sabría que su lista tenía un error. Si se dejara pasar, la cola se migraría dos veces y el informe mostraría dos entradas.
  - Los nombres de cola de RabbitMQ distinguen mayúsculas, igual que los vhost.
  - Guardar una copia evita que quien creó la lista original pueda cambiar la petición después de validarla.
- **Alternativa descartada:** eliminar los nombres repetidos sin avisar.

### Lectura de los mensajes en origen

- **Decisión:** los mensajes se recibirán de uno en uno en origen o ninguno si está vacío.
- **Motivo:**
  - RabbitMQ no ofrece por AMQP una operación para leer toda la cola de una vez; los mensajes se obtienen uno a uno.
  - Cargar todos en memoria no escala si hay miles.
  - Si algo falla a mitad, con un lote completo habría muchos mensajes ya tomados y pendientes de confirmar, y volver a un estado consistente sería muy difícil.
- **Alternativa descartada:** leer todos los mensajes como un único lote y migrarlos después.

### Migración de cola

- **Decisión:** al realizar el migrado de cola el método que migra recibe el nombre de la cola en cada llamada.
- **Motivo:** si procesamos la migración de colas y no solicitamos el nombre de cada llamada habría que crear una origen y destino distintos por cola, esto generaría ruido.
- **Alternativa descartada:** la opción era copiar las colas sin pasarlas pero esto podría dar problemas si en un futuro se quiere solicitar al usuario que usa la herramienta el poder decidir qué colas quiere migrar.

### Política ante un fallo

- **Decisión:** ante un fallo al migrar un mensaje, se para esa cola. El mensaje que falló se devuelve al origen sin confirmar, los ya movidos permanecen en el destino confirmados en el origen, y los que quedaban no se tocan. El fallo queda reflejado en el resultado de esa cola. Las demás colas continúan.
- **Motivo:**
  - Si se devolviera el mensaje y se siguiera leyendo, el origen lo entregaría otra vez y se entraría en un bucle.
  - Un fallo suele tener una causa que afecta a los siguientes mensajes (por ejemplo, el destino no disponible).
  - Seguir tras un fallo alteraría el orden de los mensajes en el destino.
  - El estado queda predecible y la migración puede reanudarse volviendo a ejecutarla.
- **Alternativa descartada:** continuar con los siguientes mensajes tras un fallo ("mover los que se puedan").
- **Fallo al confirmar en el origen:** la cola se para y el fallo queda en el resultado de esa cola. Ese mensaje no se cuenta como migrado: migrado significa publicado en el destino y confirmado en el origen.
- **Fallo al devolver el mensaje al origen:** si el destino falla y además falla la devolución del mensaje, el resultado conserva los dos errores en un `AggregateException`, primero el del destino y después el de la devolución. Quien muestre el error debe recorrer sus `InnerExceptions`. El mensaje queda sin confirmar en el origen, y RabbitMQ lo devuelve a la cola cuando se cierra el canal.
- **Fallo del origen al contar o recibir:** la cola se para y el fallo queda en el resultado de esa cola. Los mensajes ya migrados siguen contados. No hay nada que devolver, porque el mensaje que falló no llegó a recibirse.
- **Límite conocido:** si el destino confirma el mensaje pero falla la confirmación en el origen, el mensaje queda en ambas colas. El error del resultado avisa de que el último mensaje pudo quedar duplicado.

### Significado de cancelar

- **Decisión:** la cancelación se atiende entre mensajes. Si se cancela antes de empezar, no se lee ni se mueve nada. Si se cancela durante un mensaje, ese mensaje se termina (publicar y confirmar) y después se detiene. Los mensajes ya movidos siguen movidos. La cancelación se señala con `OperationCanceledException`, la convención de .NET.
- **Motivo:** cancelar entre publicar y confirmar dejaría el mensaje duplicado en las dos colas. Terminando el mensaje en curso, cada mensaje queda o en el origen o en el destino.
- **Alternativa descartada:** cancelar de inmediato, incluso en mitad de un mensaje.
- **Cómo se aplica:** el token de cancelación solo llega al origen al contar y al recibir. Publicar, confirmar y devolver un mensaje al origen no lo reciben, ni siquiera la devolución tras un fallo del destino, para que cada mensaje acabe en el origen o en el destino. Si se cancela al contar o al recibir, se lanza `OperationCanceledException` y no se guarda como error en el resultado.
- **Límite conocido:** la cancelación no es instantánea; espera a que acabe el mensaje en curso.
- **Límite conocido:** cualquier `OperationCanceledException` que lance el origen al contar o recibir se trata como una cancelación, aunque no venga del token del usuario.
