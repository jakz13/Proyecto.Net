# ADR-001: Estrategia de Resolucion de Conflictos de Sincronizacion Offline-First

## Estado
Aprobado

## Fecha
2026-11-05

## Contexto y Problematica
En la operativa de distribucion de ultima milla, los repartidores realizan sus tareas en calle bajo condiciones de conectividad celular inestable, intermitente o nula (zonas rurales, subterraneos, zonas sin cobertura). La aplicacion movil (`UltimaMilla.Mobile`) adopta un modelo offline-first donde todas las acciones operativas (confirmacion de carga, seleccion de paradas, entrega exitosa con POD, incidencias/fallos de entrega, retiros de devolucion y telemetria GPS) se persisten de inmediato en una base de datos local cifrada con SQLCipher y se encolan en una lista de operaciones pendientes (`OperacionOfflineLocal`).

Simultaneamente, los despachadores en el Backoffice de la plataforma web pueden realizar modificaciones operativas sobre hojas de ruta en curso (UC-10: reasignacion de envios, reprogramaciones de franja horaria o cancelaciones de paradas). Cuando el repartidor recupera conectividad, el motor de sincronizacion (`SyncService`) drena la cola local contra la API del backend (`src/Api`). Esto puede producir discrepancias entre el estado registrado en el dispositivo y el estado vigente en el servidor.

Se requiere una politica formal, determinista y auditada para resolver estos conflictos sin perdida de datos reales, sin corromper la cola FIFO local y preservando la trazabilidad de auditoria exigida por la normativa y los casos de uso (UC-20, UC-21, UC-39).

## Decision Arquitectonica

### 1. Claves de Idempotencia Universales (`IdempotencyKey`)
- Toda operacion encolada en el dispositivo genera un identificador unico de transaccion (`IdempotencyKey` / `OperacionId`).
- Si una solicitud llega al servidor mas de una vez por reintentos de red, el backend detecta la ejecucion previa sobre el envio o la parada y responde de forma idempotente (`200 OK` con `idempotente = true`), retornando el estado final alcanzado sin duplicar intentos de entrega, eventos ni mensajes en el Outbox.

### 2. Matriz de Decision de Conflictos

| Tipo de Conflicto | Escenario Local (Dispositivo) | Escenario Remoto (Servidor) | Politica Aplicada | Resolucion y Estado Final |
|---|---|---|---|---|
| **Entrega vs Reasignacion** | Repartidor realizo entrega fisica en calle con prueba POD (firma, foto, documento del receptor). | Despachador retiro o reasigno el envio mientras el movil estuvo offline (UC-10). | **Prevalece la Entrega Fisica Real con Evidencia (POD)** | El backend acepta la entrega, transiciona el envio a `Entregado`, cancela la reasignacion previa, crea un registro inmutable en `ConflictoSincronizacion` y añade el evento de auditoria en el historial del envio (UC-39). |
| **Fallo vs Reasignacion** | Repartidor registro visita fallida (cliente ausente, direccion inaccesible). | Despachador retiro o reasigno el envio a otra hoja de ruta o reprogramacion. | **Prevalece el Estado del Servidor** | El servidor mantiene la reasignacion/retiro planificado. Registra el intento fallido en la bitacora de auditoria del envio (`IntentoFallidoEnConflicto`) y en `ConflictoSincronizacion`. Responde con `conflictoResuelto = true` (o `409 Conflict`), permitiendo al movil marcar la parada como procesada sin bloquear la cola. |
| **Orden de Paradas vs Reprogramacion** | Repartidor altero localmente la secuencia de navegacion o uso optimizacion 2-Opt. | Despachador reprogramo o reordeno paradas desde el tablero en vivo. | **Prevalece la Ultima Accion Registrada con Sello de Tiempo (LWW)** | El backend actualiza el orden de visita segun la operacion mas reciente, registrando el timestamp del dispositivo para evaluar el trayecto real. |
| **Telemetria GPS Diferida** | Lotes de posiciones acumulados durante horas sin conexion celular. | Posiciones en vivo recibidas previamente o tablero en espera. | **Insercion Historica por Lotes** | Las posiciones se envian en bloques de hasta 500 registros (`POST /api/mobile/v1/posiciones`) y se ordenan por `TomadaEn` (timestamp del dispositivo), preservando la cronologia veridica de la ruta. |

### 3. Politica de Reintentos de Red vs Conflictos de Negocio
- **Fallas Transitorias de Red (HTTP 5xx, timeouts, caidas de conexion):** El motor `SyncService` interrumpe de inmediato el vaciado de la cola para preservar el orden FIFO y reintenta exponencialmente cuando el sistema operativo reporta conectividad restaurada (`Connectivity.ConnectivityChanged`).
- **Conflictos de Negocio o Respuestas Deterministas (HTTP 200 con `conflictoResuelto = true` o HTTP 409 Conflict):** No constituyen fallas de transporte. El motor de sincronizacion:
  1. Extrae el estado autoritativo resuelto por el servidor (`EstadoFinalResuelto`).
  2. Actualiza la base de datos local SQLite para reflejar la verdad del servidor.
  3. Marca la operacion como sincronizada (`MarcarComoSincronizadoAsync`).
  4. Continua con la siguiente operacion de la cola FIFO sin interrupciones.

### 4. Notificacion al Repartidor y Auditoria en el Backoffice
- **En la Aplicacion Movil:**
  - Cuando se resuelve un conflicto donde el servidor mantuvo su estado (ej. un envio fue retirado por el despachador), la parada local se actualiza a estado informativo (`Reasignado` / `Retirado`).
  - Si el repartidor se encuentra en linea, el canal de SignalR (`OperacionHub`) emite una alerta directa con el motivo de la modificacion.
- **En el Backoffice de Despacho:**
  - Todos los eventos de conflicto se persisten en la tabla `ConflictosSincronizacion` (`EntidadAfectada`, `EntidadId`, `EstadoServidor`, `EstadoDispositivo`, `PoliticaAplicada`, `Resolucion`, `Timestamp`).
  - El historial de trazabilidad del envio (UC-39 / `GET /api/envios/{id}/historial`) expone los eventos de resolucion de conflicto para supervision de los supervisores y comercios.

## Consecuencias

### Positivas
- Respeto incondicional de la realidad fisica en calle: un paquete entregado con firma y foto jamas se pierde ni se marca erroneamente como pendiente.
- Cola de sincronizacion auto-sanable: los conflictos de negocio no detienen ni bloquean el procesamiento de las demas operaciones del reparto.
- Trazabilidad y no repudio: cada resolucion de conflicto queda registrada en la entidad `ConflictoSincronizacion` y en la bitacora de auditoria del envio.

### Negativas / Mitigaciones
- Si un envio ya habia sido reasignado fisicamente a otro repartidor pero el primero lo entrego offline, requiere intervencion del despachador para recuperar el paquete duplicado si existiera. Mitigacion: Las reglas operativas impiden despachar un segundo bulto fisico sin confirmacion en deposito.

