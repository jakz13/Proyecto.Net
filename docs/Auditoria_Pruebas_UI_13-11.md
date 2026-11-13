# Auditoría de pruebas de interfaz, seguridad y disponibilidad (13/11/2026)

Se recorrió el sistema desplegado (`shippingbox.duckdns.org`) con un navegador real (Chromium automatizado): se presionaron los botones, se llenaron los formularios y se
miraron las pantallas, con cada perfil (administrador, despachador, operario, repartidor por API, comercio, administrador de plataforma) y con el seguimiento público.
Los datos de prueba creados (usuarios `qa.*`, `esc.*`, `lock.*`, `e2e.*`, `gui.*`, vehículos `Z…`, zonas `QA…`, comercio `QA Comercio…`) quedaron desactivados.

## Corregido en esta auditoría (todo con su prueba y desplegado)

| # | Gravedad | Hallazgo | Corrección |
|---|---|---|---|
| 1 | Crítica | Un administrador de operador podía crear un usuario con el perfil **AdministradorPlataforma** y acceder a las funcionalidades de todos los operadores | El perfil de plataforma ya no se ofrece ni se acepta desde un operador |
| 2 | Alta | Cualquier repartidor del operador podía iniciar, entregar y rendir la hoja de ruta de **otro** repartidor y ver sus destinatarios | Filtro de propiedad en todos los endpoints móviles de ruta (403) |
| 3 | Alta | `GET /api/envios` (nombres y direcciones de todos los destinatarios) lo veían el repartidor y el administrador de la plataforma | Política `ListadoDeEnvios` (administrador, despachador, operario, comercio) |
| 4 | Alta | La pantalla **Hojas de Ruta** aparecía siempre vacía (enviaba `estado=todos` a la API) | Comparación sin distinguir mayúsculas |
| 5 | Alta | **Recepción en depósito**: desmarcar todos los bultos y confirmar daba el envío por recibido "sin discrepancias" | Una lista vacía se rechaza con mensaje |
| 6 | Alta | **Zonas**: las franjas se mostraban en formato de 12 horas (13:00 → 01:00); crear o editar una zona corrompía sus franjas | Formato `HH:mm` |
| 7 | Alta | **Motivos de no entrega**: el alta siempre fallaba (validaba también los campos del formulario de edición) | Solo se validan los campos del alta |
| 8 | Alta | **Carga**: con 60 usuarios concurrentes el 32 % de las peticiones daba 5xx (`too many clients` en Postgres con `max_connections=40`) | Tope de 14 conexiones por API y 6 del worker; 0 % de error con la misma carga |
| 9 | Media | La **identidad visual** aceptaba logos y sitio web `javascript:` | Solo `http(s)`, imagen subida o `data:image/` |
| 10 | Media | El balanceador sacaba del reparto a las dos APIs bajo carga (chequeo `/ready` lento → 503) | Chequeo de vida `/live` y 4 fallos de tolerancia |
| 11 | Media | El **extravío** del Backoffice era un simulacro y el despachador recibía 403; la **rendición previa** también daba 403 | Conectado a la API real; política Despacho |
| 12 | Media | Envíos mostraba "Comercio General" en todos los envíos y el modal de extravío mostraba el estado como número | Se resuelve el comercio real; estado legible |
| 13 | Media | Mapas de seguimiento (Backoffice y Portal) **inventaban** paradas intermedias, un camión y un repartidor "Juan Pérez" | Solo se dibuja el depósito y el destino |
| 14 | Media | Los enlaces del Tablero al detalle de ruta daban 404 (`/Rutas/Detalle?id=`) | `/Rutas/Detalle/{id}` |
| 15 | Media | Portal: el filtro por estado del listado enviaba el número del estado, la API respondía 400 y la pantalla se caía | Se envía el nombre del estado |
| 16 | Baja | La incidencia creada al declarar un extravío no quedaba vinculada al envío | Vínculo por id / código de seguimiento |
| 17 | Baja | Interruptor "Simular repartidor con ruta previa sin rendir" visible en Despacho | Quitado |
| 18 | Baja | jQuery no se cargaba antes de la validación (error en consola en 15 pantallas); polígono SVG inválido en el menú | Corregidos |
| 19 | Baja | Las incidencias y los cambios de hoja de ruta no refrescaban las listas en tiempo real | Eventos `IncidenciaCambio` y `CambioRuta` |
| 20 | Baja | Botón "Sincronizar App Repartidor" mostraba un mensaje falso | Muestra la última posición real recibida |

## Observaciones sin corregir (decisión de diseño o fuera de alcance)

- **Comercio dado de baja**: sus usuarios no pueden iniciar sesión y no pueden dar de alta envíos ni retiros, pero un token ya emitido sigue pudiendo *leer* envíos, cuenta corriente y avisos (y cambiar el webhook) hasta que vence. El seguimiento público de sus envíos sigue funcionando y el operador puede seguir procesándolos.
- Falta la cabecera `Content-Security-Policy` (el resto de las cabeceras de seguridad están).
- Recepción en depósito: todos los bultos arrancan marcados como recibidos; el operario debe desmarcar los que faltan (decisión de diseño).
- El Backoffice cambia el esquema con `ALTER TABLE … LogoUrl TYPE text` en tiempo de ejecución en lugar de una migración.
- Mensajería: el interruptor "simular fallo" sigue visible (útil para la defensa).
- Con 60 usuarios concurrentes sobre endpoints pesados (`/api/envios`, tablero) la latencia p95 es de ~7 s: la VM tiene 2 vCPU.
- Las franjas de "Montevideo" y "Zona de Reparto Felipe Bernardi" tienen un tramo 02:00–06:00 que probablemente es consecuencia del error 6.

## Disponibilidad y balanceo

Caddy reparte (round robin, con cookie para SignalR) entre `api1` y `api2` y las saca del reparto si falla el chequeo de salud.

| Prueba | Resultado |
|---|---|
| Reparto normal (60 peticiones) | 28 / 32 entre las dos instancias |
| Se apaga `api1` (y luego `api2`) | 40/40 y 40/40 peticiones correctas; la primera tarda entre 0,3 y 1,3 s (reintento); todo el tráfico pasa a la otra |
| Se vuelve a levantar | Sana en ~4 s y vuelve el reparto parejo, sin intervención |
| Se apagan las dos | 502 en la API; el Backoffice sigue sirviendo su página; todo vuelve solo al levantarlas |
| Eventos en tiempo real entre instancias | Llegan aunque la petición la atienda la otra instancia (Redis) |

## Seguridad: verificado sin hallazgos

Aislamiento entre operadores (404/403), JWT con firma inválida / `alg=none` / rol elevado (401), bloqueo de cuenta al 5.º intento fallido (423), CORS, *path traversal* en logos,
POST sin token antiforgery, inyección SQL en filtros, XSS almacenado en el nombre del destinatario (portal), SSRF en webhooks (bloqueo al conectar), alta con `comercioId` ajeno
(se ignora), Swagger no expone el JSON, endpoints de prueba y `/metrics` responden 404 en producción.
