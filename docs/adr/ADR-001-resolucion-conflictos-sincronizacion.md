# ADR-001: Cómo resolvemos conflictos cuando el repartidor trabaja sin conexión

- **Estado:** Aceptado
- **Fecha:** 2026-11-05
- **Responsable:** Joaquín Bassini

## Contexto y restricciones

Cuando los repartidores trabajan en calle pierden señal con frecuencia (subterráneos, zonas alejadas, sótanos), por lo que la aplicación móvil (`UltimaMilla.Mobile`) opera *offline-first* (requerimiento 6.11): persiste cada acción en una base local cifrada (`SQLite` con `SQLCipher`) y la encola (`OperacionOfflineLocal`) para enviarla cuando recupera conectividad.

El conflicto surge cuando, mientras el repartidor está desconectado, un despachador en el Backoffice modifica la hoja de ruta (por ejemplo, reasigna o cancela un envío). Al volver la señal y drenarse la cola mediante `SyncService`, el servidor y el teléfono pueden tener versiones divergentes de la misma parada. Se necesitan reglas deterministas para decidir qué estado prevalece sin perder entregas reales, sin bloquear la cola FIFO móvil y manteniendo trazabilidad auditable (UC-20, UC-21, UC-39).

## Decisión adoptada

- **Clave única de idempotencia por operación (`IdempotencyKey`):** cada acción generada en el móvil incluye un identificador único; si por cortes de red se reintenta el envío, el servidor reconoce la clave y responde `200 OK` sin duplicar entregas, intentos ni eventos.
- **Reglas según el tipo de conflicto:**
  1. **Entrega física vs. reasignación:** si el repartidor entregó el paquete en calle con evidencia (firma, foto, documento del receptor) mientras estuvo sin señal y en el servidor se había reasignado, **prevalece la entrega física**. El envío transiciona a `Entregado` y el conflicto queda auditado.
  2. **Intento fallido vs. reasignación:** si el repartidor marcó visita fallida pero el servidor ya reasignó o canceló el envío, **prevalece el estado del servidor**. El intento fallido se preserva como registro histórico (`IntentoFallidoEnConflicto`) y el envío conserva el estado vigente en backend.
  3. **Orden de paradas:** si el orden cambia en móvil y servidor, se aplica la operación más reciente según marca de tiempo.
  4. **Posiciones GPS acumuladas sin señal:** se envían en bloques (`POST /api/mobile/v1/posiciones`) y se ordenan cronológicamente por `TomadaEn`.
- **Resiliencia de la cola FIFO local:**
  - Ante fallos de red (`5xx` o pérdida de conexión), la app pausa el drenaje para conservar el orden FIFO y reintenta al recuperar conectividad.
  - Ante respuestas de conflicto resuelto (`conflictoResuelto = true` o `409 Conflict`), la app adopta el estado autoritativo del servidor (`EstadoFinalResuelto`), marca la operación como sincronizada (`MarcarComoSincronizadoAsync`) y continúa con la siguiente operación.
- **Auditoría:** todo conflicto se persiste en `ConflictosSincronizacion` y se expone en el historial del envío en el Backoffice.

## Alternativas consideradas y motivo de su descarte

- **Exigir conectividad en línea (bloqueo pesimista):** Descartado porque incumple el requerimiento de operación sin conexión (6.11) e impide al repartidor confirmar entregas en zonas sin señal móvil.
- **Last-Write-Wins (LWW) puro por reloj para todos los casos:** Descartado porque si un repartidor registra una entrega física con firma y foto sin señal y minutos después un despachador reasigna el envío en el backoffice, LWW sobrescribiría la entrega real dejando pendiente un paquete ya entregado.
- **Resolución manual interactiva en la aplicación móvil:** Descartado porque frena el drenaje de la cola FIFO en calle y el repartidor no tiene contexto administrativo para resolver reasignaciones de despacho.

## Consecuencias

### Ventajas
- Nunca se pierde una entrega física respaldada con prueba de entrega (POD).
- La cola del dispositivo se auto-sana y continúa procesando el resto de paradas ante conflictos de negocio.
- Trazabilidad completa de cada conflicto resuelto en `ConflictosSincronizacion` y en el historial del envío.

### Desventajas y mitigaciones
- Si un envío se entregó sin conexión luego de haber sido reasignado a otro repartidor, el despachador debe gestionar operativamente la devolución de un posible segundo bulto; como mitigación, en depósito no se despacha un bulto reasignado sin verificación física previa.

### Qué dificulta a futuro
- **Reglas configurables por cliente o resolución campo a campo (CRDT):** al fijar una jerarquía única de precedencia (`Entrega > Reasignación > IntentoFallido`) dentro de los handlers, admitir en el futuro flujos de aprobación manual o fusión parcial por atributos exigiría versionar propiedades individuales tanto en el modelo móvil como en los contratos de sincronización del backend.
- **Crecimiento histórico de claves de idempotencia:** sostener idempotencia estricta sobre reintentos diferidos obliga a indexar y retener `IdempotencyKey`, requiriendo a mayor escala políticas de depuración o particionamiento temporal para no degradar los índices.
