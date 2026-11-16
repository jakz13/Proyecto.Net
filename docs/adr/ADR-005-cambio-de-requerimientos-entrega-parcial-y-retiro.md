# ADR-005: Cómo incorporamos la entrega parcial por bulto y el retiro en domicilio

**Estado:** Aprobado · **Fecha:** 2026-11-09 · **Responsables:** Francisco Lema (2.1) y Anthony Curbelo (2.2) · **Requerimientos:** RF 31 a RF 41

## 1. Contexto y problema
El 30/10 se sumaron dos cambios de alcance obligatorios para equipos de 4 personas:
- **Entrega parcial (2.1):** cada bulto de un envío puede tener un resultado distinto (ej. se entregan 2 de 3) y el estado del envío se calcula según sus bultos.
- **Retiro en domicilio (2.2):** un flujo nuevo donde el repartidor retira paquetes en la casa de un comprador para llevarlos al comercio.

El desafío era sumar ambos cambios sin romper la máquina de estados, el versionado de reglas, el Outbox, el modo sin conexión del celular ni el aislamiento entre operadores y comercios.

## 2. Decisión
- **Para entrega parcial (2.1):**
  - Se creó una máquina de estados propia por bulto (`MaquinaEstadosBulto`) y el estado `EntregadoParcialmente` en `Envio`, derivado automáticamente de sus bultos.
  - La configuración de si se admite entrega parcial y qué hacer con los bultos restantes (`AdmiteEntregaParcial`, `PoliticaEntregaParcial`) se guardó en `ReglaOperativa` versionada.
  - En el seguimiento público se muestra el progreso (ej. *"2 de 3 bultos entregados"*) sin exponer IDs internos, y los avisos a comercios salen con el contrato `v2`.
- **Para retiro en domicilio (2.2):**
  - Se modeló como un agregado propio (`Retiro`, `BultoRetiro`, `EventoRetiro`) con su propia máquina de estados (`MaquinaEstadosRetiro`) y tarifa propia de modalidad `Retiro`, en lugar de forzarlo dentro de `Envio`.
  - **Hojas de ruta mixtas:** una parada puede ser de entrega o de retiro, y el control de capacidad del vehículo (`PerfilCargaRuta`) recorre la ruta en orden porque los bultos retirados van ocupando lugar a medida que se suben.
  - **Offline, Outbox y multitenancy:** reutiliza el mismo Outbox, el seguimiento público (con prefijo `RET-`), los filtros por operador/comercio y tiene su propia tabla de resolución de conflictos donde prevalece lo ocurrido en el domicilio.

## 3. Alternativas consideradas
- **Modelar el retiro como un `Envio` "al revés":** descartada, porque tienen estados, reglas y tarifas distintas y habría llenado el código de `if/else` difíciles de mantener.
- **Guardar datos nuevos en memoria para evitar migraciones de base de datos:** se probó al principio de 2.1 pero se descartó y corrigió, porque los datos se perdían al reiniciar y no se compartían entre `api1`, `api2` y el Worker. Todo pasó a columnas reales con migración de EF Core.

## 4. Impacto sobre contratos de API y eventos de integración (Sección 2.3)
- **Versionado de contratos de avisos a comercios (`v1` vs `v2`):**
  - El contrato `v1` se conservó intacto (`avisoId`, `evento`, `envioId`, `codigoSeguimiento`, `estadoAnterior`, `estado`, `ocurridoEn`) para no romper las integraciones existentes de los comercios.
  - Se incorporó el contrato `v2` (`SuscripcionAviso.ContratoV2`), que agrega `versionContrato: "v2"`, `tipoEventoDominio`, `resumenBultos` (`totalBultos`, `bultosEntregados`, `bultosPendientes`, `avance`) y el arreglo `bultos` con `{ identificacion, estado, motivoNoEntrega }`.
  - Para retiros en domicilio se publica `EventoRetiroPublicado` con código `RET-` y resumen de bultos retirados.
- **Contratos de API y Seguimiento Público:**
  - `SeguimientoPublicoDto` sumó de forma retrocompatible `TotalBultos`, `BultosEntregados`, `BultosPendientes`, `AvanceEntregaBultos`, `Tipo` (`Envio` | `Retiro`) y `EstadoRetiro`, reutilizando el mismo endpoint público `/api/seguimiento/{codigo}` sin exponer PII ni IDs internos.
  - En la API móvil se agregaron endpoints específicos (`/entrega-parcial`, `/retiro`, `/retiro/no-realizado`) manteniendo intacto `/entrega-exitosa`.

## 5. Reflexión: cómo las decisiones de diseño previas facilitaron o dificultaron el cambio
- **Qué facilitó la incorporación:**
  - **Clean Architecture + Agregados ricos (ADR 01):** encapsular las invariantes en `Envio`, `Bulto` y `Retiro` permitió agregar `MaquinaEstadosBulto` y `MaquinaEstadosRetiro` sin dispersar validaciones en endpoints ni páginas.
  - **Filtros globales de EF Core (`IMustHaveTenant` / `IMustHaveComercioTenant`):** al hacer que `Retiro` implemente ambas interfaces, el aislamiento de dos niveles (operador y comercio) funcionó automáticamente en todas las consultas.
  - **Patrón Outbox transaccional e idempotencia en Worker (`ADR-004`):** publicar los nuevos eventos de entrega parcial y retiro reutilizó la misma tabla `OutboxMessages`, el mismo consumidor idempotente y la misma firma HMAC.
  - **Cola offline basada en comandos con `IdempotencyKey` (`ADR-001`):** agregar `ENTREGA_PARCIAL`, `RETIRO_REALIZADO` y `RETIRO_NO_REALIZADO` en SQLite cifrado requirió solo nuevos tipos de payload en `SyncService`.
- **Qué dificultó la incorporación y requirió adaptación:**
  - **Suposición inicial `1 Parada = 1 Envío`:** originalmente `Parada.EnvioId` era `Guid` obligatorio y las consultas de rutas, despacho, tablero en vivo y carga móvil asumían que toda parada tenía un `Envio` que salía desde el depósito. Pasar a hojas de ruta mixtas obligó a volver `EnvioId` anulable, agregar `Tipo` y `RetiroId`, evaluar la capacidad del vehículo punto a punto (`PerfilCargaRuta`, ya que las entregas liberan espacio y los retiros lo ocupan) y excluir los retiros del escaneo de carga en depósito.
  - **Derivación de estado envío ↔ bultos:** antes el estado del envío se asignaba de forma directa; con entregas parciales hubo que coordinar la transición de cada `Bulto` con `MaquinaEstadosBulto.DerivarEstadoEnvio` y contemplar bultos pendientes en segundas visitas, rendiciones, devoluciones y liquidaciones.

## 6. Consecuencias
- **Ventajas:** el código de retiros quedó limpio y separado; si mañana apareciera un tercer tipo de parada, la estructura ya lo soporta. Tanto los envíos (incluidas las entregas parciales cerradas y sus recargos por segunda visita) como los retiros entregados al comercio se integran en la liquidación y reportes del operador.
- **Desventajas y cómo se mitigan:** todas las consultas de rutas, tablero y reportes tuvieron que adaptarse para distinguir paradas de entrega vs. paradas de retiro, cubierto mediante pruebas de dominio, aplicación e integración.
