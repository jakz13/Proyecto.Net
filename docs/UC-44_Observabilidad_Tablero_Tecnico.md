# UC-44 – Observabilidad y tablero técnico (Serilog + OpenTelemetry + Prometheus + Grafana)

> Número provisional: ajustarlo a la matriz de casos de uso del equipo si UC-44 ya está tomado.
> Documento autocontenido: pensado para pasárselo tal cual a un asistente de IA que implemente el caso de uso.

## 1. Identificación

| Campo | Valor |
|---|---|
| **ID** | UC-44 |
| **Nombre** | Observabilidad de la plataforma y tablero técnico |
| **Requerimiento de la letra** | §6.12 Observabilidad; hito del 5/11 ("tablero de observabilidad operativo"); §6.13 (puntos de salud, ya hechos) |
| **Actor principal** | Operador técnico / Administrador de la plataforma (quien monitorea el sistema, no el usuario de negocio) |
| **Actores secundarios** | Sistema (API, Worker), App del repartidor (origina el identificador de correlación), Prometheus, Grafana |
| **Prioridad** | Alta (bloquea la demostración del hito del 5/11) |
| **Tipo** | Requerimiento no funcional transversal |

## 2. Objetivo

Que cualquier integrante pueda, desde un navegador y con usuario/contraseña, ver el estado técnico del sistema desplegado en la nube y **seguir una operación de extremo a extremo** (app móvil → API → cola → worker) mediante un identificador de correlación.

La letra exige textualmente: *"un identificador de correlación que permita seguir una operación de extremo a extremo, desde su origen en la aplicación móvil hasta su procesamiento en el worker, y al menos un tablero técnico que muestre tiempo medio de respuesta, percentiles 95 y 99 de latencia, tasa de errores, profundidad de la cola, sincronizaciones pendientes, avisos a comercios fallidos y relación de aciertos del caché."*

## 3. Alcance y reparto de trabajo

Este caso de uso se divide en dos partes para poder trabajar en paralelo sin pisarse:

| Parte | Responsable | Contenido |
|---|---|---|
| **A. Instrumentación en código** | Felipe (con su asistente de IA) | Secciones 6, 7 y 8: Serilog, OpenTelemetry, correlación, métricas de negocio, endpoint `/metrics`, dashboard de Grafana como archivo JSON, pruebas |
| **B. Infraestructura del tablero** | Anthony | Servicios `prometheus` y `grafana` en `docker-compose.prod.yml`, regla en Caddy para `panel.shippingbox.duckdns.org`, ampliación de `scripts/desplegar.ps1`, despliegue en la VM |

Felipe **no** debe tocar `docker-compose.prod.yml`, `docker/Caddyfile.prod` ni `scripts/`. Sí debe entregar los archivos de configuración que esos servicios van a montar (secciones 8.2 y 8.3) en las rutas indicadas.

## 4. Precondiciones

1. La API y el Worker compilan y arrancan (`docker compose up` local y despliegue en la VM funcionando).
2. Existen PostgreSQL, Redis y RabbitMQ accesibles desde la API y el Worker.
3. El entorno local **no** pide usuario ni contraseña (modo demo). Esa conducta no debe cambiar: la observabilidad es transparente para el desarrollo local.
4. Se respetan las reglas del repositorio (`.agents/rules`): Clean Architecture, código y comentarios en español, Conventional Commits en español, sin marcas de herramientas en los commits.

## 5. Disparador

Se ejecuta de forma continua: cada solicitud HTTP, cada mensaje procesado por el Worker y cada operación de caché genera telemetría. El actor consulta el tablero cuando necesita ver el estado.

## 6. Flujo principal – instrumentación (Parte A)

1. **Logs estructurados (Serilog).** La API y el Worker usan Serilog con salida a consola en JSON compacto (`Serilog.Formatting.Compact`). Cada evento incluye `CorrelationId`, `TraceId`, `TenantId` (si existe) y el nombre del servicio. Los datos personales se enmascaran (regla 05 del repositorio). Nivel por defecto `Information`; `Microsoft.AspNetCore` en `Warning`.
2. **Identificador de correlación.**
   - Un middleware de la API lee el encabezado `X-Correlation-Id`. Si no viene, genera uno nuevo (GUID). Lo devuelve en la respuesta, lo agrega al `LogContext` de Serilog y a la `Activity` actual (`Activity.Current?.SetTag("correlation_id", ...)`).
   - Cuando un caso de uso publica un evento (Outbox), el `CorrelationId` se guarda en el mensaje. **El código ya lo transporta** en `EventoEnvioPublicado.CorrelationId`, `IMensajeria` y `RabbitMqMessagePublisher`; verificar que se llene con el valor del middleware y no solo con el `TraceId`.
   - El Worker, al consumir un mensaje, toma ese `CorrelationId` y lo pone en el `LogContext` durante todo el procesamiento.
   - **App móvil:** la app debe enviar `X-Correlation-Id` (un GUID por lote de sincronización o por operación) en sus llamadas HTTP. Es un cambio mínimo en el `DelegatingHandler` de la app; coordinarlo con Joaquín. El portal del comercio ya lo envía (`JwtAuthorizationHandler`).
3. **Métricas y trazas (OpenTelemetry).**
   - Paquetes: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, `OpenTelemetry.Exporter.Prometheus.AspNetCore` (versiones fijas, estables, anteriores a dos semanas).
   - API: instrumentación de ASP.NET Core, HttpClient y Runtime; exponer **`/metrics`** (Prometheus).
   - Worker: `/metrics` en un puerto propio (por ejemplo 9464) con un listener mínimo, ya que el Worker no es un host web. Alternativa aceptable: `OpenTelemetry.Exporter.Prometheus.HttpListener`.
   - Trazas: activar el `ActivitySource` de la aplicación y las instrumentaciones; exportar por OTLP **solo si** `OTEL_EXPORTER_OTLP_ENDPOINT` está configurado (en la VM no se despliega Jaeger por falta de memoria; las trazas quedan correlacionadas a través de `TraceId` en los logs).
4. **Métricas de negocio (las que pide la letra).** Definir un único `Meter` llamado `UltimaMilla` y estas métricas:

   | Métrica (nombre Prometheus) | Tipo | Origen | Para qué panel |
   |---|---|---|---|
   | `http_server_request_duration_seconds` (la entrega la instrumentación de ASP.NET Core) | Histograma | API | Tiempo medio, p95 y p99 de latencia, tasa de errores (por código de estado) |
   | `um_cola_mensajes_pendientes` | Gauge | Worker (consulta cantidad de mensajes listos en la cola de RabbitMQ y de `OutboxMessage` sin publicar) | Profundidad de la cola |
   | `um_sync_eventos_pendientes` | Gauge | API/Worker: eventos recibidos desde la app móvil que aún no fueron procesados (outbox pendiente de origen móvil) | Sincronizaciones pendientes |
   | `um_sync_eventos_recibidos_total` | Counter | API (endpoints de sincronización móvil) | Volumen de sincronización |
   | `um_avisos_comercio_fallidos` | Gauge | Worker (cantidad de `AvisoComercio` en estado Fallido) | Avisos a comercios fallidos |
   | `um_avisos_comercio_intentos_total{resultado="ok\|error"}` | Counter | Worker (cada intento de entrega) | Tasa de avisos fallidos |
   | `um_cache_aciertos_total` / `um_cache_fallos_total` | Counter | `DistributedCacheService` (aciertos cuando `GetStringAsync` devuelve dato; fallos cuando devuelve nulo) | Relación de aciertos del caché = aciertos / (aciertos + fallos) |

   - **Arquitectura limpia:** la Application no depende de OpenTelemetry. Crear en `Application/Ports` una interfaz `IMetricasNegocio` (por ejemplo `RegistrarAciertoCache()`, `RegistrarFalloCache()`, `RegistrarIntentoAviso(bool exito)`, `RegistrarEventoSincronizacion(int cantidad)`) con implementación en `Infrastructure` usando `System.Diagnostics.Metrics`. Los gauges se leen con callbacks (`ObservableGauge`) que consultan la base/cola de forma económica (consulta `COUNT` con índice, cacheada unos segundos).
   - El proyecto de pruebas de arquitectura (NetArchTest) debe seguir en verde.
5. **Salud ya existente.** `/health`, `/ready` y `/live` están hechos y no se tocan.

## 7. Flujos alternativos y manejo de errores

| Situación | Comportamiento esperado |
|---|---|
| RabbitMQ o Redis no disponibles al medir un gauge | El gauge devuelve el último valor conocido (o no emite muestra); nunca lanza excepción que afecte al endpoint `/metrics` ni a la API |
| `/metrics` consultado desde internet | **Debe estar bloqueado en el proxy público.** Solo lo lee Prometheus dentro de la red interna de Docker (Anthony lo resuelve en Caddy). Felipe no lo protege en código, pero no debe agregar datos sensibles a las etiquetas |
| Falta el encabezado `X-Correlation-Id` | Se genera uno nuevo y se devuelve en la respuesta |
| Encabezado `X-Correlation-Id` con más de 64 caracteres o caracteres no imprimibles | Se descarta y se genera uno nuevo (evita inyección en logs) |
| Alta cardinalidad | Prohibido usar `envioId`, `usuarioId`, `codigoSeguimiento` o el correlationId como etiqueta de métrica. Solo etiquetas acotadas: método, ruta con plantilla, código de estado, resultado, tipo de evento |
| Entorno local (modo demo) | `/metrics` y los logs funcionan igual, sin autenticación, sin configurar nada |

## 8. Entregables concretos (Parte A)

### 8.1 Código

| Capa | Archivo / acción |
|---|---|
| Api | `Program.cs`: Serilog (`UseSerilog`), OpenTelemetry (`AddOpenTelemetry().WithMetrics().WithTracing()`), middleware de correlación, `app.MapPrometheusScrapingEndpoint("/metrics")` |
| Api | `Observabilidad/CorrelationIdMiddleware.cs` (nuevo) |
| Worker | `Program.cs`: Serilog y OpenTelemetry con exportador Prometheus en el puerto 9464; correlación al consumir mensajes en `EventConsumerBackgroundService` |
| Application | `Ports/IMetricasNegocio.cs` (nuevo) |
| Infrastructure | `Observabilidad/MetricasNegocio.cs` (nuevo, implementa el puerto y define el `Meter` "UltimaMilla"); instrumentar `DistributedCacheService` con aciertos/fallos |
| Infrastructure / Worker | Registrar los gauges de cola, sincronización pendiente y avisos fallidos |
| Mobile (coordinar con Joaquín) | Enviar `X-Correlation-Id` en el `DelegatingHandler` |

### 8.2 Configuración de Prometheus (entregar como archivo)

`docker/prometheus/prometheus.yml` con `scrape_interval: 15s` y tres trabajos: `api` (objetivos `api1:8080` y `api2:8080`, ruta `/metrics`), `worker` (`worker:9464`) y `rabbitmq` (`rabbitmq:15692`, plugin `rabbitmq_prometheus` ya incluido en la imagen; Anthony lo habilita). Etiqueta `instance` distinta para api1 y api2, así el tablero puede comparar instancias.

### 8.3 Tablero de Grafana como código (entregar como archivos)

- `docker/grafana/provisioning/datasources/prometheus.yml` (fuente de datos Prometheus, `url: http://prometheus:9090`).
- `docker/grafana/provisioning/dashboards/dashboards.yml` (carga los JSON de la carpeta).
- `docker/grafana/dashboards/ultima-milla-tecnico.json`: un dashboard llamado **"Última Milla – Tablero técnico"** con estos paneles (consultas de referencia):

| Panel | Consulta PromQL de referencia |
|---|---|
| Tiempo medio de respuesta | `sum(rate(http_server_request_duration_seconds_sum[5m])) / sum(rate(http_server_request_duration_seconds_count[5m]))` |
| Latencia p95 | `histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))` |
| Latencia p99 | `histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m])))` |
| Tasa de errores | `sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])) / sum(rate(http_server_request_duration_seconds_count[5m]))` |
| Profundidad de la cola | `um_cola_mensajes_pendientes` |
| Sincronizaciones pendientes | `um_sync_eventos_pendientes` |
| Avisos a comercios fallidos | `um_avisos_comercio_fallidos` |
| Relación de aciertos del caché | `sum(rate(um_cache_aciertos_total[5m])) / (sum(rate(um_cache_aciertos_total[5m])) + sum(rate(um_cache_fallos_total[5m])))` |
| Extra recomendado | Solicitudes por segundo por instancia (api1 vs api2) para mostrar el balanceo; estado `up` de cada servicio |

Cuidar que los nombres de métricas coincidan con los que realmente expone el exportador (revisar `/metrics` en local; los sufijos y las unidades pueden variar según la versión) y ajustar las consultas.

### 8.4 Pruebas

- Prueba unitaria del middleware de correlación: sin encabezado genera uno; con encabezado válido lo conserva; con encabezado inválido lo reemplaza; siempre lo devuelve en la respuesta.
- Prueba unitaria de `DistributedCacheService`: acierto incrementa aciertos; fallo incrementa fallos.
- Prueba de integración: `GET /metrics` responde 200 y contiene `http_server_request_duration_seconds`.
- Las pruebas existentes (Application, Domain, Integration, Frontend, arquitectura) deben seguir en verde.

## 9. Restricciones técnicas

- **Memoria de la VM de producción: 1 GB en total, ya casi llena** (queda ~350 MB disponibles contando la caché, más 4 GB de swap). Por eso: no agregar Jaeger, Seq, Loki ni el Collector de OpenTelemetry. Presupuesto para los dos servicios nuevos: Prometheus ≤ 96 MB (retención de 24 h), Grafana ≤ 160 MB. La instrumentación en la API debe agregar un consumo despreciable (la API usa unos 75 MB de 256 MB).
- Versiones de paquetes fijas, estables y publicadas hace más de dos semanas.
- Sin secretos en el repositorio: la contraseña de Grafana se toma de `.env.prod` (variable `GRAFANA_PASSWORD`, la agrega Anthony).
- No cambiar el modo demo local ni el comportamiento de autenticación.
- Toda la documentación y los comentarios en español; commits en español con Conventional Commits y sin marcas de herramientas.

## 10. Postcondiciones

- La API y el Worker emiten logs JSON con `CorrelationId` y exponen `/metrics`.
- Un `CorrelationId` generado por la app o el portal aparece en los logs de la API **y** en los del Worker del mismo evento.
- El tablero "Última Milla – Tablero técnico" se carga automáticamente al levantar Grafana (provisioning), sin configuración manual.
- Los siete indicadores exigidos por la letra se ven con datos reales.

## 11. Criterios de aceptación (demostración del hito)

1. Abrir `https://panel.shippingbox.duckdns.org`, iniciar sesión y ver el tablero con los siete indicadores.
2. Generar tráfico (varias solicitudes desde el portal o el backoffice) y ver que suben las curvas de latencia y de solicitudes por instancia; se ven **api1 y api2** por separado.
3. Detener una instancia de la API (`docker stop shippingbox-api1-1`) y mostrar que el sitio sigue respondiendo y que el tablero marca esa instancia como caída.
4. Seguir una operación de extremo a extremo: enviar una solicitud con `X-Correlation-Id: demo-001`, cambiar el estado de un envío y encontrar `demo-001` en los logs de la API y del Worker (`docker logs ... | grep demo-001`).
5. Con el receptor de prueba de avisos en modo falla, ver crecer "Avisos a comercios fallidos"; al restablecerlo, ver que baja.
6. Todas las pruebas automáticas pasan.
7. Localmente, `docker compose up` sigue sin pedir usuario ni contraseña.

## 12. Fuera de alcance

Jaeger/Tempo, alertas por correo, retención prolongada de métricas, tableros por tenant, y el despliegue en la VM (Parte B, a cargo de Anthony).

## 13. Referencias

- Letra del laboratorio: §6.12, §6.13, §8.3 (hito del 5/11).
- `Documento de Análisis y Diseño.md`, sección 6 (Observabilidad: Serilog, OpenTelemetry, Grafana/Prometheus).
- `docs/plan_implementacion_worker.md` (Worker, CorrelationId, métricas de cola y fallos).
- Código existente relacionado: `src/Application/Messaging/EventoEnvioPublicado.cs`, `src/Infrastructure/Messaging/RabbitMqMessagePublisher.cs`, `src/Infrastructure/Services/DistributedCacheService.cs`, `src/Worker/BackgroundServices/EventConsumerBackgroundService.cs`, `src/Web/PortalComercio.Client/Auth/JwtAuthorizationHandler.cs`.
