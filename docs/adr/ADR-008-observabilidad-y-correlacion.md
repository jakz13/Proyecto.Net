# ADR-008: Observabilidad, identificador de correlación y tablero técnico

**Estado:** aprobado · **Responsables:** Felipe Bernardi (tablero) y Anthony Curbelo (correlación) · **Requerimiento:** 6.12

## 1. Contexto y restricciones
Hay que poder seguir una operación de extremo a extremo, desde la aplicación móvil hasta el worker, y ver en un tablero el tiempo medio de respuesta, los percentiles 95 y 99, la tasa de errores, la profundidad de la cola, las sincronizaciones pendientes, los avisos fallidos y la relación de aciertos del caché. La instrumentación debe usar Serilog y OpenTelemetry, en una VM de 1 GB.

## 2. Decisión
- **Registro estructurado con Serilog** en JSON, con el identificador de correlación en cada línea y datos sensibles enmascarados.
- **Identificador de correlación:** `CorrelationIdMiddleware` toma `X-Correlation-Id` (o la traza actual si no viene), lo devuelve en la respuesta y lo propaga a los logs, a la unidad de trabajo y al mensaje del outbox. El worker lo recupera del mensaje, de modo que el mismo identificador aparece en la API y en el worker (se muestra con `scripts/demo-correlacion.ps1`). Cada solicitud HTTP de la API se registra con su identificador.
- **Métricas con OpenTelemetry** (medidores `UltimaMilla` y `UltimaMilla.Cache`) expuestas en `/metrics`, que **no es público** (Caddy no lo publica; Prometheus lo lee por la red interna). Prometheus (retención de 24 h y 300 MB) y Grafana aprovisionados desde archivos del repositorio.
- **Tablero técnico** «Última Milla – Técnico» con 13 consultas sobre los indicadores exigidos, instancias activas incluidas.

## 3. Alternativas consideradas
- **Seq, Jaeger o el panel de .NET Aspire:** válidos; se eligió Prometheus + Grafana por ser livianos, estándar y aprovisionables como código.
- **Servicio administrado de la nube:** descartado por costo.
- **Trazas distribuidas con exportador OTLP a un colector:** habilitado por configuración pero no desplegado, por memoria.

## 4. Consecuencias
- El tablero es parte del repositorio: se reconstruye con el entorno. Un cambio de configuración de Grafana o Prometheus requiere reiniciar esos servicios (lo hace el script de despliegue).
- Con 1 GB de RAM la retención de métricas es corta (24 h).
- **Pendiente:** la aplicación móvil debe enviar `X-Correlation-Id` para que el seguimiento empiece realmente en el teléfono; hoy empieza en la API.
