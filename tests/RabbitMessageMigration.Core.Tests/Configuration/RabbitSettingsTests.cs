 // Pruebas para la configuración de RabbitMQ.
 // 1. Cuando host es nulo entonces lanzar una excepción de tipo ArgumentNullException.
 // 2. Cuando host es "" entonces lanzar una excepción de tipo ArgumentException.
 // 3. Cuando host es " " entonces lanzar una excepción de tipo ArgumentException.
 // 4. Cuando el host es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
 // 5. Cuando el puerto sea <= 0 entonces lanzar una excepción de tipo ArgumentOutOfRangeException.
 // 6. Cuando el puerto sea > 65535 entonces lanzar una excepción de tipo ArgumentOutOfRangeException.
 // 7. Cuando el puerto es > 0 y <= 65535 entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
 // 8. Cuando el usuario sea nulo entonces lanzar una excepción de tipo ArgumentNullException
 // 9. Cuando el usuario sea "" entonces lanzar una excepción de tipo ArgumentException.
 // 10. Cuando el usuario sea " " entonces lanzar una excepción de tipo ArgumentException.
 // 11. Cuando el usuario es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
 // 12. Cuando la contraseña sea nula entonces lanzar una excepción de tipo ArgumentNullException.
 // 13. Cuando la contraseña sea "" entonces lanzar una excepción de tipo ArgumentException.
 // 14. Cuando la contraseña sea " " entonces lanzar una excepción de tipo ArgumentException.
 // 15. Cuando la contraseña es válida entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
 // 16. Cuando Vhost sea " " entonces lanzar una excepción de tipo ArgumentException.
 // 17. Cuando Vhost sea nulo o "" entonces el Vhost debe ser "/". 
 // 18. Cuando Vhost es válido entonces se construye correctamente la configuración de RabbitMQ y queda guardado.
 // 19. Cuando se convierte la configuración a texto entonces el texto no contiene la contraseña (usar una contraseña larga y reconocible).
 // 20. Cuando se convierte la configuración a texto entonces el texto contiene el host.

