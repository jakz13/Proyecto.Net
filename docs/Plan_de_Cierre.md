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
- [ ] Cola de Fallidos: la implementa Felipe (frontend); backend no toca nada. Hoy existe como pantalla con botón en el menú

## Pendientes del traspaso (no mobile)
- [x] Subir lo local: `main` y `desarrollo-backend` en `de0e986` (10/11). CI de GitHub probado una vez: sigue bloqueado (`billing issue`). Despliegue manual con `scripts\desplegar.ps1` (ver registro)
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
- 10/11 — Resumen para Joaquín: `docs/Pendientes_Mobile.md`.
- 10/11 — `git push` a `main` (de0e986): el CI falló de inmediato por la cuenta (billing). Despliegue manual lanzado con `scripts/desplegar.ps1`.

## Opcionales: meta 16 puntos
| Opcional | Pts | Estado | Responsable |
|---|---|---|---|
| Cobertura > 70 % | 2 | Cumplido (76 %) | — |
| API pública | 3 | Cumplido | — |
| Resiliencia | 2 | Cumplido | — |
| Carga | 2 | Falta ejecutarla sobre el ambiente remoto | Backend (tras el despliegue) |
| Geocercas | 3 | Falta análisis de batería | Joaquín |
| Optimización de paradas | 2 | Falta tabla medida | Joaquín |
| **Subtotal reclamado** | **14** | | |
| Pruebas E2E con Playwright | 2 | Pendiente (evaluar) | Backend/Frontend |
| Banderas de funcionalidad | 2 | Backend hecho; falta la pantalla. Decisión previa: no reclamar | Reevaluar si hace falta margen |
- 10/11 — Despliegue manual terminado (api1, api2, worker, backoffice, portal, panel): `/ready` 200 en API y Portal; los endpoints nuevos responden 401 sin token (existen en producción). Falta: probar en vivo con la UI (firmas, selector de operadores, CSV) y la carga sobre el ambiente remoto.
