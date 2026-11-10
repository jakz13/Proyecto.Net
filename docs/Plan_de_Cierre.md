# Plan de cierre (constancia de trabajo y retoma)

Documento vivo. **Marcar cada casilla en cuanto se termine la tarea.** Si una conversación nueva retoma esto, leer primero este archivo, luego `docs/Traspaso_Pendientes.md` y `docs/Estado_de_Requerimientos.md`.

## Contexto y acuerdos (10/11/2026)
- Fechas: monitoreo 12/11, entrega final 15/11, defensa 19/11.
- La parte **mobile es de Joaquín**: no tocar la rama `mobile` ni `src/Mobile`. Solo se le entrega un resumen de lo que le falta (sección final).
- Meta de opcionales: **al menos 16 puntos** (la letra pide 12; margen por si alguno se invalida).
- Despliegue: probar una vez el CI de GitHub (bloqueado por la cuenta, `billing issue`). Si sigue fallando, desplegar a mano con el script que compila en la máquina local y sube imágenes (la VM de Oracle tiene 1 GB de RAM y 1 núcleo: compilar allí tarda ~25 min, no es viable). Sin parches temporales.
- Convenciones: español, commits `tipo(ámbito): resumen` sin marcas de herramientas, no abrir PRs sin pedido, no usar `despacho@gmail.com` en pruebas.
- Banderas de funcionalidad: NO se reclaman como opcional (decisión previa; ver `docs/Estado_de_Requerimientos.md`). Reevaluar solo si hace falta para llegar a 16 puntos.

## Checklist del equipo (notas de los compañeros)
Backend
- [x] Firmas de entrega visibles en Backoffice y en los roles que corresponda — `GET /api/envios/{id}/intentos/{intento}/evidencia/{firma|foto}` (política `ConsultaDeEnvios`: admin, despachador, operario y comercio dueño), modal de trazabilidad del Backoffice y detalle del Portal; pruebas `ObtenerEvidenciaEntregaTests` y `AutorizacionPorPerfilApiTests`
- [x] Extraviados llegan como notificación al Portal de Comercio — el handler emite `EnvioCambioEstado` al grupo del comercio (alerta "Paquete Extraviado" en `Envios.razor`) y el evento `EnvioExtraviado` al outbox (aviso webhook, configurable); cubierto por `MarcarEnvioExtraviadoCommandHandlerTests` y `TableroRealtimeNotificationHandlerTests`. No se probó en navegador
- [x] Mismo RUT en dos operadores: el comercio ve a ambos en tiempo real — `GET /api/comercio/operadores` y `POST /api/comercio/operadores/{id}/sesion` (token acotado a un operador), aviso SignalR `OperadoresDelComercioCambiaron` al vincular/suspender, selector real en el Portal (antes era un selector de demo y en el servidor mostraba operadores inventados). Pruebas `ComercioConVariosOperadoresApiTests`
- [x] Subida de imágenes de identidad visual (hecho por el equipo)
- [x] CSV del portal: `ImportacionCsvApiTests` (archivo real, error por fila, reimportación idempotente, `;`, permisos). Webhooks: el receptor de prueba es `/api/testing/webhook-receiver` (en el servidor, host interno `http://api1:8080/api/testing/webhook-receiver` por `Avisos__HostsPermitidos`); falta solo probarlo en vivo con la UI
- [x] Cuenta corriente del portal: ahora muestra el operador, subtotal e IVA por liquidación (antes solo el total); el retiro entregado no entra en la liquidación (la letra no lo exige)

Frontend
- [ ] Quitar etiquetas "Observabilidad" y "Tablero de Operaciones"
- [x] Seguimiento público: panel informativo de devolución (En devolución / Devuelto) con qué hacer. Decisión: NO hay acción pública que cambie el estado (la letra no la pide, el comercio solicita retiros desde el portal, y un endpoint anónimo que cambie estados es un vector de abuso)
- [x] Cola de Fallidos: se mantiene como pantalla (la letra pide gestión de fallidos); ya tiene botón en el menú

## Pendientes del traspaso (no mobile)
- [ ] Subir y desplegar lo local (9 commits sin subir)
- [ ] Carga y resiliencia sobre el ambiente remoto
- [ ] Terraform: `import` + `plan` sin cambios y captura de costos (requiere `oci session authenticate`: lo hace el usuario)
- [ ] Opcionales hasta 16 puntos (ver sección)

## Hallazgos y correcciones del 10/11
- Defecto corregido: los usuarios sembrados del segundo operador (`admin.express@...`) no podían iniciar sesión (500 por hash en otro formato). La semilla ahora usa `PasswordHasherService`, repara los hashes viejos y el verificador rechaza hashes en formato inválido sin lanzar excepción (`PasswordHasherServiceTests`).
- El detalle del envío (`/api/envios/{id}/detalle`) pasó de `RequireAuthorization()` a la política `ConsultaDeEnvios` (el repartidor ya no entra).

## Registro de avance
(Anotar aquí cada hito con fecha y commit.)
- 10/11 — Firmas visibles (commit `cb307cd`).
- 10/11 — Comercio con varios operadores + corrección del hash de la semilla.
