# ADR-004: Consistencia entre la persistencia y la publicación de eventos (outbox), idempotencia y avisos a comercios

**Estado:** aprobado · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.8, 6.9, cambio 2.1 (RF 36) y 2.2 (RF 41)

## 1. Contexto y restricciones
Cada cambio de estado de un envío o de un retiro debe llegar al worker (notificación al destinatario, avisos HTTP a los comercios, indicadores) sin que pueda persistirse un cambio sin su evento ni publicarse un evento de un cambio que no se guardó. El broker puede estar caído, los mensajes pueden llegar dos veces y el receptor del comercio puede fallar. Los comercios ya reciben avisos con un formato que no se puede romper.

## 2. Decisión
- **Patrón outbox transaccional.** El evento se escribe en la tabla `OutboxMessages` **en la misma transacción** que el cambio. Lo hace la propia unidad de trabajo (`EfUnitOfWork`): al guardar, por cada `EventoEnvio` o `EventoRetiro` nuevo agrega su mensaje. Ningún caso de uso publica a mano, por lo que un flujo nuevo no puede olvidarse de publicar.
- **Publicador independiente:** un proceso lee el outbox y publica en RabbitMQ; un fallo del broker no pierde nada, el mensaje queda pendiente.
- **Idempotencia en el consumidor:** el worker guarda una clave por mensaje (`ClavesIdempotenciaConsumidor`) en la misma transacción que sus efectos; reprocesar el mismo mensaje devuelve «duplicado» y no repite avisos ni notificaciones.
- **Reintentos con espera creciente y cola de fallidos**, visible y reintentable desde el backoffice (UC-42).
- **Avisos a comercios:** suscripción por comercio y tipo de evento, firma HMAC del cuerpo, reintentos con espera creciente, cola de fallidos y panel de historial con reenvío en el portal. Se entrega un receptor de prueba (`/api/testing/webhook-receiver`).
- **Contrato versionado por suscripción.** `v1` conserva exactamente el formato original; `v2` agrega el detalle por bulto (entrega parcial) y **es el único que recibe los eventos de retiro**. Así los comercios que ya integraban `v1` no se ven afectados por funcionalidades nuevas.

## 3. Alternativas consideradas
- **Publicar directamente al broker tras guardar (doble escritura):** descartada; si el proceso cae entre ambas operaciones se pierde el evento.
- **Transacción distribuida (2PC):** descartada; RabbitMQ no la soporta de forma práctica y agrega acoplamiento.
- **Captura de cambios (CDC, p. ej. Debezium):** válida pero sobredimensionada para el entorno de una VM gratuita de 1 GB.
- **Un solo contrato con campos nuevos opcionales:** descartada; cambia el cuerpo firmado y puede romper receptores estrictos.

## 4. Consecuencias
- La entrega es **al menos una vez**; la idempotencia del consumidor evita los efectos duplicados.
- Existe una latencia de segundos entre el cambio y el aviso (el publicador sondea el outbox).
- El outbox crece y hoy **no se purga** lo ya procesado: la limpieza periódica es una tarea pendiente (con el volumen de la demo no es un problema).
- Mantener dos versiones de contrato cuesta código, pero es el precio de no romper integraciones existentes; una versión nueva requiere decidir qué eventos viaja en cuál.
