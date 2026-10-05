## Decisiones de diseño

### Vhost de `RabbitSettings`

- **Decisión:** si el `Vhost` es `null` o `""`, se entiende que quien usa la herramienta quiere el vhost por defecto y se usa `"/"`. Si el `Vhost` contiene solo espacios (`" "`), se lanza una `ArgumentException`.
- **Motivo:** dejar el vhost vacío significa "no he indicado ninguno", mientras que escribir solo espacios u otras teclas es, con mucha probabilidad, un error al teclear.
- **Alternativa descartada:** tratar `" "` igual que `""` y usar `"/"`. Ocultaría el error de tecleo y la herramienta, que mueve mensajes, podría moverlos desde o hacia el vhost `"/"` sin que quien la usa lo hubiera pedido.

### Contraseña de `RabbitSettings`

- **Decisión:** al convertir la configuración a texto (`ToString`, interpolación de strings, `Console.WriteLine`), la contraseña no se muestra. La propiedad sigue guardando el valor real, porque RabbitMQ lo necesita para conectar.
- **Motivo:** evitar que la contraseña acabe en pantalla o en un log sin que nadie lo haya querido.
- **Límite conocido:** no protege si alguien lee la propiedad de la contraseña y la imprime directamente.
