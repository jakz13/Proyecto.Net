# Registros de decisión de arquitectura

Cada registro tiene cuatro partes: contexto y restricciones, decisión, alternativas consideradas y consecuencias.

| Decisión (tema obligatorio de la letra §8.2) | Registro | Responsable |
|---|---|---|
| Estilo arquitectónico y organización del código | ADR 01 en [`Documento de Análisis y Diseño.md`](../../Documento%20de%20An%C3%A1lisis%20y%20Dise%C3%B1o.md) | Equipo |
| Estrategia de multitenancy | Primera sección del [`Documento de Análisis y Diseño.md`](../../Documento%20de%20An%C3%A1lisis%20y%20Dise%C3%B1o.md) | Equipo |
| Modo de renderizado de Blazor | ADR 02 en [`Documento de Análisis y Diseño.md`](../../Documento%20de%20An%C3%A1lisis%20y%20Dise%C3%B1o.md) | Felipe Bernardi |
| Política de resolución de conflictos de sincronización | [ADR-001](ADR-001-resolucion-conflictos-sincronizacion.md) | Joaquín Bassini |
| Estrategia de caché e invalidación | [ADR-003](ADR-003-cache-distribuido-e-invalidacion.md) | Francisco Lema |
| Consistencia entre persistencia y publicación de eventos | [ADR-004](ADR-004-outbox-idempotencia-y-avisos-a-comercios.md) | Anthony Curbelo |
| **Cambio de requerimientos** (entrega parcial y retiro en domicilio) | [ADR-005](ADR-005-cambio-de-requerimientos-entrega-parcial-y-retiro.md) | Francisco Lema y Anthony Curbelo |

Otras decisiones registradas:

| Tema | Registro | Responsable |
|---|---|---|
| Notificaciones móviles: descarte de FCM y alternativa | [ADR-002](ADR-002-notificaciones-moviles-fcm.md) | Joaquín Bassini |
| Autorización por perfil y aislamiento de dos niveles | [ADR-006](ADR-006-autorizacion-por-perfil-y-aislamiento.md) | Anthony Curbelo |
| Despliegue, infraestructura como código y balanceo | [ADR-007](ADR-007-despliegue-iac-y-balanceo.md) | Anthony Curbelo |
| Observabilidad y correlación | [ADR-008](ADR-008-observabilidad-y-correlacion.md) | Felipe Bernardi y Anthony Curbelo |

La numeración de los dos primeros registros del documento de análisis (ADR 01 y ADR 02) es anterior a esta carpeta y se conserva por compatibilidad con la entrega del 4 de octubre.
