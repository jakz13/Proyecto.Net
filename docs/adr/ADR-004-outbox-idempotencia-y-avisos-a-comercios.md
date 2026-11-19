# ADR-004: Cómo aseguramos que los eventos no se pierdan ni se dupliquen (Outbox y avisos a comercios)

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.8, 6.9, RF 36 y RF 41

## 1. Contexto y problema
Cada vez que cambia el estado de un envío o de un retiro, hay que avisar al Worker para que envíe notificaciones al destinatario, mande avisos HTTP (webhooks) a los comercios y actualice indicadores.

Si guardáramos en la base de datos y después publicáramos directo a RabbitMQ, un corte entre medio dejaría la base actualizada pero sin evento publicado (o al revés). Además, RabbitMQ puede entregar un mensaje más de una vez, los servidores de los comercios pueden estar caídos temporalmente y los comercios ya integrados esperan un formato de aviso que no podemos romper.

## 2. Decisión
- **Patrón Outbox en la misma transacción:** cuando se guarda un cambio en `EfUnitOfWork`, por cada `EventoEnvio` o `EventoRetiro` nuevo se inserta automáticamente una fila en la tabla `OutboxMessages` dentro de la misma transacción de PostgreSQL. Así ningún caso de uso puede olvidarse de publicar.
- **Publicador independiente en el Worker:** `OutboxPublisherBackgroundService` lee esa tabla cada 3 segundos y publica en RabbitMQ (`ultimamilla.eventos`). Si RabbitMQ está caído, los mensajes quedan guardados esperando hasta que vuelva.
- **Consumidor idempotente:** antes de procesar un mensaje, el Worker guarda su identificador en `ClavesIdempotenciaConsumidor` dentro de la misma transacción que sus efectos. Si el mismo mensaje llega dos veces, se detecta como duplicado y no manda dos correos ni dos avisos.
- **Reintentos y cola de fallidos:** si falla el procesamiento, RabbitMQ reintenta 3 veces con esperas crecientes (5 s, 20 s y 60 s). Si sigue fallando, pasa a `ultimamilla.eventos.fallidos` y se guarda en `MensajesFallidos` para verlo y reintentarlo desde el Backoffice.
- **Avisos a comercios con versión de contrato:** cada comercio se suscribe por tipo de evento, recibe el mensaje firmado con HMAC, tiene hasta 4 intentos (esperas de 1, 5 y 15 minutos) y un panel para ver historial y reenviar. Mantenemos dos versiones de contrato: `v1` (formato original) y `v2` (agrega detalle por bulto y eventos de retiro), además de un receptor de prueba (`/api/testing/webhook-receiver`).
- **Cola de fallidos de los avisos:** un 500 (o cualquier error) del comercio no es un fallo de procesamiento del mensaje, así que no pasa por `MensajesFallidosCola`: el aviso lleva su propio estado (`Reintentando` → `Agotado` tras el cuarto intento, unos 21 minutos). Los avisos `Agotado` o `Fallido` aparecen en la misma pantalla de cola de fallidos del Backoffice (tipo `NotificacionWebhookComercio`) leyendo el estado del propio aviso, sin duplicarlo en otra tabla. Allí el administrador los **reenvía** (no se reencola el evento original: el consumidor lo vería como duplicado) o los **descarta** con un motivo (`Descartado`), que los saca de la cola y del contador de avisos fallidos del tablero técnico. El "Ping" del portal hace una llamada real por el mismo emisor y refleja el código que devuelve el comercio.

## 3. Alternativas consideradas
- **Publicar directo a RabbitMQ después de guardar en BD:** descartada, porque si se cae la red o el broker justo después del commit, el evento se pierde para siempre.
- **Transacciones distribuidas (2PC):** descartada, porque RabbitMQ no las soporta de forma práctica y complica mucho la operación.
- **Herramientas pesadas de captura de cambios (Debezium):** descartada por ser excesiva para los recursos de nuestra VM.
- **Un único contrato de webhook cambiando campos:** descartada, porque cambiar el JSON firmado rompería la validación de los comercios que ya usan `v1`.

## 4. Consecuencias
- **Ventajas:** garantía de entrega "al menos una vez" sin efectos duplicados, tolerancia a caídas de RabbitMQ o de comercios externos y compatibilidad con integraciones viejas.
- **Desventajas y cómo se mitigan:** hay una demora pequeña (unos segundos) entre que se guarda el cambio y se envía el aviso, y la tabla `OutboxMessages` crece con el tiempo (como mejora futura conviene agregar una tarea de limpieza de filas viejas ya procesadas).
