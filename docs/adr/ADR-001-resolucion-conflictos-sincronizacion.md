# ADR-001: Cómo resolvemos conflictos cuando el repartidor trabaja sin conexión

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsable:** Joaquín Bassini · **Requerimientos:** 6.11, UC-20, UC-21, UC-39

## 1. Contexto y problema
Cuando los repartidores están en la calle, muchas veces pierden señal (subterráneos, zonas alejadas, sótanos). Por eso la aplicación móvil (`UltimaMilla.Mobile`) funciona primero sin conexión (*offline-first*): guarda cada acción en una base local cifrada en el teléfono (`SQLite` con `SQLCipher`) y la deja en una cola para enviarla cuando vuelva internet.

El problema aparece cuando, mientras el repartidor está desconectado, un despachador desde el Backoffice cambia esa misma hoja de ruta (por ejemplo, reasigna o cancela un envío). Cuando vuelve la señal y la app envía sus datos pendientes (`SyncService`), el servidor y el teléfono pueden tener versiones distintas de la misma parada. Necesitábamos reglas claras para decidir qué dato gana sin perder entregas reales ni trancar la cola del teléfono.

## 2. Decisión
- **Clave única por operación (`IdempotencyKey`):** cada acción que el repartidor hace en el teléfono genera un identificador único. Si por un corte de red el envío se repite, el servidor reconoce esa clave y responde `200 OK` sin duplicar la entrega ni los eventos.
- **Reglas según el tipo de conflicto:**
  1. **Entrega física vs. reasignación:** si el repartidor entregó el paquete en la calle con evidencia (firma, foto, documento) mientras estuvo sin señal y en el servidor se había reasignado, **gana la entrega física**. El envío pasa a `Entregado` y el conflicto queda registrado para auditoría.
  2. **Intento fallido vs. reasignación:** si el repartidor marcó visita fallida pero el servidor ya reasignó o canceló el envío, **gana el servidor**. Se guarda el intento fallido como registro histórico (`IntentoFallidoEnConflicto`), pero el envío mantiene el estado del servidor.
  3. **Orden de paradas:** si cambia el orden localmente y en el servidor, se toma la acción más reciente según la marca de tiempo.
  4. **Posiciones GPS guardadas sin señal:** se envían en bloques de hasta 500 puntos (`POST /api/mobile/v1/posiciones`) y el servidor las ordena por la hora real en que se tomaron (`TomadaEn`).
- **La cola del teléfono no se tranca:**
  - Si hay un fallo de red (`5xx` o sin internet), la app pausa la cola y reintenta cuando vuelve la conexión.
  - Si el servidor responde que hubo un conflicto resuelto (`conflictoResuelto = true` o `409 Conflict`), la app actualiza su base local con la respuesta del servidor, marca la tarea como sincronizada y sigue con la siguiente.
- **Auditoría:** cada conflicto resuelto se guarda en la tabla `ConflictosSincronizacion` y aparece en el historial del envío en el Backoffice.

## 3. Alternativas consideradas
- **Que siempre gane el servidor:** descartada, porque si un repartidor ya entregó un paquete con firma y foto sin señal, borrar esa entrega dejaría el sistema mintiendo sobre la realidad en la calle.
- **Que siempre gane el teléfono:** descartada, porque un intento fallido viejo podría pisar una reasignación válida hecha por el despachador.
- **Frenar la cola ante cualquier `409 Conflict`:** descartada, porque un solo conflicto bloquearía todas las entregas posteriores del día en el teléfono.

## 4. Consecuencias
- **Ventajas:** nunca se pierde una entrega real con evidencia, la cola del celular sigue avanzando sola aunque haya conflictos y todo queda auditado.
- **Desventajas y cómo se mitigan:** si un envío se entregó sin conexión justo después de haberse reasignado a otro repartidor, el despachador debe intervenir manualmente. Para reducir ese riesgo, en depósito no se despacha un segundo bulto físico sin confirmación previa.
