# ADR-008: Cómo registramos logs, métricas y trazabilidad de punta a punta

**Estado:** Aprobado · **Fecha:** 2026-11-05 · **Responsables:** Felipe Bernardi (tablero) y Anthony Curbelo (correlación) · **Requerimiento:** 6.12

## 1. Contexto y problema
Con dos instancias de la API, un Worker y RabbitMQ trabajando en paralelo, si algo falla o tarda mucho necesitamos poder seguir una operación de principio a fin (desde la app móvil hasta el Worker) y ver en un tablero técnico la salud del sistema: latencia media, p95, p99, tasa de errores, cola de mensajes, sincronizaciones pendientes, avisos fallidos y aciertos del caché. Todo esto sin consumir demasiada memoria en el servidor.

## 2. Decisión
- **Logs estructurados con Serilog:** se guardan en formato JSON, enmascarando automáticamente datos sensibles (como correos o documentos) y adjuntando el identificador de correlación en cada línea.
- **Identificador de correlación (`X-Correlation-Id`):**
  - La app móvil genera y envía `X-Correlation-Id` en cada petición desde `AuthDelegatingHandler`.
  - En la API, `CorrelationIdMiddleware` lo toma (o crea uno si no vino), lo devuelve en la respuesta y lo propaga a los logs, a la unidad de trabajo y al mensaje del Outbox.
  - El Worker lo lee del mensaje de RabbitMQ y lo escribe en sus propios logs, permitiendo seguir el mismo pedido de punta a punta (demostrable con `scripts/demo-correlacion.ps1`).
- **Métricas con OpenTelemetry, Prometheus y Grafana:**
  - La API y el Worker (puerto interno `9464`) exponen `/metrics` solo en la red interna de Docker (Caddy no lo publica a internet).
  - Prometheus recolecta las métricas con retención de **24 horas y 300 MB** para cuidar el disco y la RAM.
  - Grafana carga automáticamente desde el repositorio el tablero **"Última Milla – Tablero técnico"** (`ultima-milla-tecnico.json`), con 9 paneles que cubren todas las métricas pedidas por la letra.

## 3. Alternativas consideradas
- **Seq, Jaeger o el panel de .NET Aspire:** buenas opciones, pero elegimos Prometheus + Grafana porque son muy livianos, estándar en producción y fáciles de dejar configurados en archivos del repositorio.
- **Servicios pagos o administrados en la nube:** descartados por costo.
- **Colector de trazas distribuidas (OTLP):** quedó preparado por configuración, pero no se desplegó en el servidor para ahorrar memoria.

## 4. Consecuencias
- **Ventajas:** el tablero técnico se reconstruye automáticamente junto con los contenedores, los datos sensibles no se filtran en logs y cualquier operación puede rastrearse desde el celular hasta el Worker.
- **Desventajas y cómo se mitigan:** la retención de métricas es corta (24 horas) por el límite de memoria del servidor, pero suficiente para monitoreo en tiempo real y demostraciones; si se edita el JSON del tablero, alcanza con reiniciar el contenedor de Grafana para aplicarlo.
