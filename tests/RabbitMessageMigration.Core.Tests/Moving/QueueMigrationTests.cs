// ¿Qué lleva un mensaje para migrar al destino y confirmarse en el origen?

// TODO: Refinar la lista

// Cuando la cola tiene un mensaje, entonces se mueve el mensaje al destino y se confirma en el origen.
// Cuando la cola está vacia, entonces el movimiento termina sin errores y no se mueve ningún mensaje.
// Cuando un mensaje se mueve y el destino falla, entonces no se confirma en el origen y se devuelve.
// Cuando hay más mensajes de los contados al empezar, entonces se mueven los contados y se confirma en el origen.
// Cuando hay varios mensajes y uno falla a mitad, entonces se mueven los que se puedan y se confirma en el origen los que se movieron.
// Cuando se cancela la operación, entonces se detiene el movimiento y no se mueve ningún mensaje.


