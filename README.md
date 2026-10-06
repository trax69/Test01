## Decisiones de diseño

### Vhost de `RabbitSettings`

- **Decisión:** si el `Vhost` es `null` o `""`, se entiende que quien usa la herramienta quiere el vhost por defecto y se usa `"/"`. Si el `Vhost` contiene solo espacios (`" "`), se lanza una `ArgumentException`.
- **Motivo:** dejar el vhost vacío significa "no he indicado ninguno", mientras que escribir solo espacios u otras teclas es, con mucha probabilidad, un error al teclear.
- **Alternativa descartada:** tratar `" "` igual que `""` y usar `"/"`. Ocultaría el error de tecleo y la herramienta, que mueve mensajes, podría moverlos desde o hacia el vhost `"/"` sin que quien la usa lo hubiera pedido.

### Contraseña de `RabbitSettings`

- **Decisión:** al convertir la configuración a texto (`ToString`, interpolación de strings, `Console.WriteLine`), la contraseña no se muestra. La propiedad sigue guardando el valor real, porque RabbitMQ lo necesita para conectar.
- **Motivo:** evitar que la contraseña acabe en pantalla o en un log sin que nadie lo haya querido.
- **Límite conocido:** no protege si alguien lee la propiedad de la contraseña y la imprime directamente.

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
- **Límite conocido:** si el destino confirma el mensaje pero falla la confirmación en el origen, el mensaje queda en ambas colas.

### Significado de cancelar

- **Decisión:** la cancelación se atiende entre mensajes. Si se cancela antes de empezar, no se lee ni se mueve nada. Si se cancela durante un mensaje, ese mensaje se termina (publicar y confirmar) y después se detiene. Los mensajes ya movidos siguen movidos. La cancelación se señala con `OperationCanceledException`, la convención de .NET.
- **Motivo:** cancelar entre publicar y confirmar dejaría el mensaje duplicado en las dos colas. Terminando el mensaje en curso, cada mensaje queda o en el origen o en el destino.
- **Alternativa descartada:** cancelar de inmediato, incluso en mitad de un mensaje.
- **Límite conocido:** la cancelación no es instantánea; espera a que acabe el mensaje en curso.
