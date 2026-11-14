# Auditoría nocturna 14/11/2026 — informe vivo

Objetivo: probar de forma sistemática el sistema desplegado en Azure antes de la defensa del jueves 19/11 a las 15:00 (docente: Nico), registrando cada prueba, su resultado esperado, el real y el veredicto.
Continúa la [auditoría de interfaz del 13/11](../Auditoria_Pruebas_UI_13-11.md); no repite lo ya cubierto salvo para reverificarlo.

## Método
Cada prueba es una combinación de condiciones (identidad, perfil, operador, comercio, estado del recurso, forma y tipo de los datos). No son solo correcto/incorrecto: se prueban todos los valores relevantes de cada condición.
Cada fila: `ID | Área | Condiciones | Esperado | Real | Veredicto (OK / DEFECTO / OBS)`.
Datos de prueba con prefijo `AUDIT-`; se eliminan al final (ver "Limpieza").

## Reglas de trabajo
- No se reescribe la historia de git anterior a `d65d984`; solo commits nuevos (`git revert` si algo sale mal).
- Ninguna credencial ni token en este documento ni en el repositorio.
- Cada defecto se documenta con su solución; si se corrige, con su prueba.

## Flujos pedidos por el docente (Nico)
| # | Flujo | Estado |
|---|---|---|
| F1 | Interrumpir una instancia de la API con tráfico y verificar continuidad | pendiente |
| F2 | App móvil sin conectividad: entregas e incidencias, y restablecer | pendiente |
| F3 | Conflicto de sincronización y aplicación de la política definida | pendiente |
| F4 | Dos operadores con configuración distinta y aislamiento | pendiente |
| F5 | Modificar tarifa y regla operativa en caliente | pendiente |
| F6 | Detener el receptor de avisos de un comercio, generar eventos, reactivar y recuperar | pendiente |
| F7 | Destruir y reconstruir el ambiente con IaC | pendiente |

## Trazabilidad con la letra (Edición 2026)
Cada prueba se etiqueta con el punto de la letra que verifica (RF 1-30, RNF 6.1-6.15, escenarios 9.3, opcionales 7). Brechas que el propio
[Estado de requerimientos](../Estado_de_Requerimientos.md) declara abiertas y que esta auditoría debe cerrar o dejar evidenciadas:

| Punto de la letra | Brecha conocida | Acción en la auditoría |
|---|---|---|
| 6.11 notificaciones con la app cerrada | ADR-002 usa servicio en primer plano, no cubre del todo la app cerrada | documentar el límite y la justificación; no inventar cobertura |
| 6.12 correlación desde la app móvil | la app aún no envía `X-Correlation-Id` | verificar el flujo con cabecera manual y reportar |
| 7.2 pruebas de carga | falta la sincronización simultánea sobre el ambiente remoto | ejecutarla acotada (costo) y registrar el resultado |
| 7.4 resiliencia | repetir la interrupción sobre una instancia en producción | cubierta por F1 |
| 6.14 IaC | `terraform.tfstate` solo en la PC | evaluar respaldo del estado y documentar |
| 6.3 límite de tasa | cuenta por instancia (con 2 instancias es el doble) | medir y documentar |
| 6.8 outbox | no se purga el outbox procesado | medir crecimiento y documentar |
| RF 5.1 (7) | liquidación de retiros no incluida (la letra no lo exige) | observación |
| 6.14 costo | falta evidencia del costo del ambiente | estimar y dejar evidencia aquí |
| 7.3 geocercas | tabla de consumo de batería sin medir en teléfono | fuera del alcance remoto; se anota |

Escenarios 9.3 anunciados = F1 a F7. Además la defensa incluye escenarios no anunciados y una modificación menor en el momento:
la auditoría ensaya cambios pequeños (por ejemplo una regla o un campo) para comprobar dónde interviene cada componente.
**Fecha límite próxima:** la entrega final vence el domingo 15/11 a las 23:59 (documento técnico, ADR, bitácora de IA, registro de horas, presentación, README).

## Resultados por prueba

### Ronda 1 (14/11 00:45 en adelante)

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R1-00a | Autenticación (6.3) | login de las 7 cuentas demo | token con rol, operador, comercio | los 7 inician sesión; claims correctos (operador 1 y 2 distintos; comercio con `comercio_id`; plataforma sin permisos de operador) | OK |
| R1-00b | Inventario | análisis estático de `src/Api` | toda ruta con política | 169 endpoints: 12 anónimos (login, integración por clave, seguimiento público, identidad visual) y 3 sin política explícita (`/api/funcionalidades`, `/api/mobile/v1/ping`, `/swagger-custom.css`) | OK (los dos primeros dan 401 sin token: protege la política por defecto) |
| R1-01 | Autorización por perfil (6.3) | 169 endpoints × 8 identidades (sin token + 7 perfiles) = 1.192 casos | sin token 401; perfil no permitido 403; permitido ≠ 401/403 | 1.162 OK; 30 marcados, todos explicados como artefactos de la prueba (ver nota) y reconfirmados a mano; 0 respuestas 5xx, 0 respuestas 429 | OK |
| R1-02 | Autorización (6.3) | `POST /api/zonas-cobertura/validar-franjas` con cuerpo `[]` × 8 identidades | solo administrador | sin token 401; admin y admin2 200; los otros 5 perfiles 403 | OK |
| R1-03 | Autorización (6.3, RF 7) | `POST /api/comercio/envios/importacion` multipart × 8 identidades | solo comercio | sin token 401; comercio 400 (archivo sin filas); los otros 6 perfiles 403 | OK |
| R1-04 | Superficie pública (6.3) | `/metrics`, `/swagger/v1/swagger.json`, `/api/testing/webhook-receiver/*`, path traversal en logos | no expuestos | todos 404 o 400; `/live`, `/ready`, `/health` responden 200 | OK |
| R1-05 | Infraestructura (6.14) | `az vm list`, `az resource list`, SSH a la VM | una sola VM, ocho recursos gestionados por Terraform | una VM (`vm-ultimamilla-prod`, B2als_v2); 12 contenedores sanos; 4 dominios 200 | OK |

Nota de R1-01: los 30 casos marcados eran (a) rutas duplicadas en grupos distintos que mi analizador mezcló (`/api/retiros/{id}/cancelar` del comercio y del operador, `/api/liquidaciones/{id}/pagar`, `DELETE /api/comercio/webhooks/suscripciones/{id}`), (b) endpoints que existen solo si se habilitan (`/api/testing/webhook-receiver`, 404 en producción por diseño) y (c) cuerpo `{}` en endpoints con binding de lista o multipart (415/400 antes del filtro de escritura). Ninguno es una falla de autorización.

### Hallazgos de herramienta (no del sistema)
- La laptop entró en *espera moderna* de 00:57 a 01:12 pese al anti-suspensión: `ES_SYSTEM_REQUIRED` no basta en Windows 11 con pantalla apagada. Corregido con `ES_DISPLAY_REQUIRED` (pantalla activa). La tapa debe quedar abierta y la laptop enchufada.

### Ronda 2

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R2-01 | Limitación de tasa (6.3) | agotar el cupo del seguimiento público desde la PC y consultar de inmediato desde la VM (otra IP pública) | cupos independientes por cliente | la PC recibe 429 a partir del pedido #20; la VM recibe **429 en el primer pedido** | **DEFECTO D-01** |
| R2-02 | Calidad (6.15) | suite completa del backend con el arreglo de D-01 | sin regresiones | 1.199 pruebas, 0 fallas (203 dominio, 790 aplicación, 10 arquitectura, 15 Backoffice, 181 integración) | OK |
| R2-03 | Secretos (6.3) | búsqueda en archivos versionados y en todo el historial de git: claves privadas, tokens, contraseñas literales, `.env`, `.tfstate` | ninguno | el token de DuckDNS y la clave demo vigente nunca se versionaron; sin claves privadas, `.env` ni `.tfstate` (este último ignorado por `infra/terraform-azure/.gitignore`); los `.tfvars` no tienen secretos; `appsettings.json` trae solo `postgres/postgres` de desarrollo local | OK |
| R2-04 | Secretos (6.3) | contraseñas demo anteriores (`Admin.2026!` y similares) | no están en el código vigente y no sirven | aparecen en 20 commits del historial; el login con la contraseña vieja da 401 y con la vigente entra | OBS-01 (baja: historial; sin efecto, ya no valen) |
| R2-06 | Limitación de tasa (6.3) | verificación de D-01 en producción tras el despliegue: agotar el cupo desde la PC y consultar desde la VM | cupos independientes | PC: 60 pasan y 20 reciben 429; VM: 6 de 6 pasan (404), ningún 429 | OK (D-01 resuelto) |
| R2-07 | Limitación de tasa (6.3) | 80 pedidos con un `X-Forwarded-For` distinto en cada uno, a través de Caddy | no se puede evadir el límite suplantando la IP | 60 pasan y 20 reciben 429, igual que sin cabecera | OK |
| R2-05 | Dependencias (6.3) | `dotnet list package --vulnerable --include-transitive` sobre los 13 proyectos | sin vulnerabilidades | ninguna vulnerabilidad conocida | OK |

### Ronda 3 — datos malformados

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R3-01 | Robustez de la API (6.4, 6.15) | todos los endpoints de escritura con identidad autorizada (≈ 90) × 13 cargas rotas (`null`, `[]`, texto, número, `{}`, tipos cambiados, campos nulos, JSON roto, 200 KB, inyección SQL y XSS, anidación de 150 niveles, Unicode con nulos y marcas bidireccionales, extremos numéricos y de fecha) + sin cuerpo + `text/plain` = 1.229 casos | 4xx controlado, sin 5xx, sin filtrar la pila, sin guardar basura | 697 × 400, 357 × 404, 135 × 415, 37 × 200; **0 respuestas 5xx; 0 respuestas con pila o internos**; la comparación de la base antes y después (52 tablas, conteo exacto) **no muestra ninguna fila nueva ni borrada** | OK |
| R3-02 | Robustez (6.4) | los 37 casos 200: `POST /api/comercio/clave-api`, `POST /api/liquidaciones/generar`, `POST /api/operacion/procesos/recalcular-indicadores` | los tres no leen el cuerpo, o no hay datos que generar | confirmado: ninguno depende del cuerpo; `liquidaciones/generar` no creó filas (0 liquidaciones antes y después) | OK |
| R3-03 | Cliente de prueba | 3 casos HTTP 0 con cuerpo de 200 KB | 4xx | artefacto de PowerShell 5.1; con `curl` los tres dan 404 en menos de 0,6 s | OK |

Efecto secundario de la prueba (OBS-03): `POST /api/comercio/clave-api` **regenera la clave de API del comercio sin necesitar cuerpo ni confirmación**, así que el barrido la rotó unas 13 veces para `comercio@gmail.com`. La VM no define `SEED_API_KEY_DEMO`, por lo que no había una clave fija conocida. Si la demostración de la API pública usa una clave guardada, **hay que regenerarla desde el portal antes de la defensa**.

### Ronda 4 — mutaciones de un campo sobre altas

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R4-01 | RF 7-9 (alta de envío) | `POST /api/envios` como comercio, 77 casos: cada campo con nulo, vacío, espacios, tipo erróneo, longitud 300/1.000/5.000, HTML, Unicode, correo con CRLF, pesos y dimensiones negativos, cero y absurdos, 200 bultos, coordenadas fuera de rango, GUID inexistentes, propiedad duplicada, JSON con comentario y coma final | 400 controlado; 201 solo con datos válidos | **Prueba inválida:** el caso base devolvió 400 porque producción geolocaliza la dirección y `Calle Audit 1` no cae en una zona; las 76 respuestas 400 pueden estar ocultando validaciones de campo (se repite con una dirección real, R4-02) | NO CONCLUYENTE |
| R4-01b | RF 7-9 | mismo barrido, caso `"bultos":[null]` | 400 | **HTTP 500** "Ocurrió un error inesperado" | **DEFECTO D-02** |

### Ronda 4 (continuación) — listas con elementos nulos, vacíos y mixtos

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R4-03 | Robustez (6.4, 6.15) | todos los endpoints de escritura × 7 cargas con **todas** las propiedades de lista a la vez (`[null]`, `[{}]`, `["x"]`, `[123]`, mixta, no-lista, `[[null]]`): 553 casos | 400 controlado, sin 5xx, sin guardar basura | 365 × 400, 173 × 404, 7 × 415, **6 × 500** (retiros, envíos, cuadros tarifarios, posiciones, crear y editar zona), **2 × 2xx con basura** (cuadro tarifario y posiciones con `[{}]`); la base cambió solo en 3 filas (1 versión de cuadro, 1 regla, 1 posición) | **DEFECTOS D-02, D-03, D-04, D-05** |
| R4-04 | Validación de rangos (RF 23) | `POST /api/mobile/v1/posiciones` con latitud 999, longitud −999, 1e308, velocidad −50, batería 500 y −10 | 400 | 400 con mensaje claro en los seis casos | OK |
| R4-05 | Validación de rangos (RF 23) | velocidad 99.999 km/h, `tomadaEn` año 1 y año 2999 | 400 o saneado | 200 en los tres (el año 2999 se acota a la hora del servidor; velocidad y año 1 se guardan tal cual) | OBS-04 (baja) |
| R4-06 | Alta de envío (RF 7, 9) | direcciones reales contra la cobertura y la tarifa de producción | alta correcta | `18 de Julio 1234` cae en cobertura; `Av. Italia 3000` y `Rambla República de México 5500` quedan fuera (400 claro); el alta fallaba por falta de regla tarifaria (ver incidente) | OK |
| R4-07 | Tarifa en caliente (RF 3, 6.6) | HTML real de `/Tarifas/NuevaVersion` en producción con la tarifa vigente de tres reglas (Estandar, Retiro, Urgente) | cada lista desplegable marca la modalidad de su regla | **ninguna de las tres tiene opción marcada**: un navegador enviaría "Estándar 24-48h" en las tres | **DEFECTO D-06** |

### Incidente provocado por la auditoría (14/11 01:31, restaurado)
El barrido R4-03 envió `{"reglasTarifa":[{}]}` a `POST /api/cuadros-tarifarios`: la API publicó la **versión 2** con una sola regla armada con los valores por defecto ("Estándar 24-48h", hasta 5 kg, $180). Esa versión desplazó a la real (Estandar $150, Retiro $120, Urgente $300) y **todas las altas de envío del operador 1 pasaron a fallar** con "No existe una regla de tarifa aplicable". Se detectó a los 3 minutos, se publicó la **versión 3** con las tres reglas originales y se comprobó un alta real ($150, estado Admitido). El historial conserva las tres versiones; los envíos ya procesados mantienen su tarifa. Fue consecuencia del propio defecto D-04.

### Ronda 5 — escenario F5 con la pantalla real (tarifa en caliente)

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R5-01 | Verificación en producción de D-02 a D-05 | 8 cuerpos rotos (bulto nulo en envío y retiro, franja nula en zona y validar-franjas, regla nula y vacía en cuadro, posición nula y vacía) tras desplegar | 400 en todos y sin cambios | 8 × 400; la versión vigente del cuadro no cambió | OK |
| R5-02 | F5 (RF 3, 6.6): publicar una tarifa **desde el formulario del Backoffice** | login real, formulario con los valores que enviaría un navegador, precio de Estandar 150 → 160 | nueva versión; el siguiente alta cotiza 160 | con la tarifa real el formulario devuelve **200 sin mensaje** y no publica | **DEFECTO D-07** |
| R5-03 | F5 | mismo formulario con el volumen máximo de las tres reglas en 1.000 | publica | 302 → versión 4: Estandar 160, Urgente 300, Retiro 120 (modalidades conservadas por el arreglo de D-06); el alta siguiente cotiza **160**; efecto inmediato | OK |
| R5-04 | Restauración | versión 5 por API con las tres reglas originales y volumen 1.000.000.000 | tarifa original vigente | vigente v5: Estandar 150, Urgente 300, Retiro 120 | OK |

### Ronda 5 (continuación) — F5, mitad "regla operativa en caliente"

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R5-05 | F5 desde la pantalla, tarifa (tras desplegar D-06 y D-07) | formulario real de producción **sin ajustes**: Estandar 150 → 170, alta, y vuelta a 150 | publica; el alta cotiza el precio nuevo | 302 en ambos sentidos; v6 = 170 y v7 = 150; las altas posteriores cotizaron 170 y 150; Urgente y Retiro intactos | OK |
| R5-06 | F5 desde la pantalla, regla operativa | `/ReglasOperativas/NuevaVersion` enviada **como un navegador** (todos los campos en orden, nombres repetidos), sin cambiar nada | la versión nueva conserva los valores vigentes | la versión 2 cambió la prueba de entrega ("Firma digital y documento del receptor" → **"Completa"**, firma + foto) y la política de devolución ("Retorno a depósito central…" → "Custodia por 72 horas…") | **DEFECTO D-08** |
| R5-07 | Restauración | publicación por API con los valores originales | regla original vigente | v3: devolución original; prueba "Firma" (forma canónica que guarda el dominio) | OK |
| R5-08 | Formularios del Backoffice (RNF 6.2) | 13 pantallas con formularios: listas sin opción marcada y nombres de campo repetidos | ninguna lista edita un valor existente sin marcarlo | solo D-06 y D-08; el resto son altas (primera opción = valor por defecto) y mini-formularios por fila; los modales de edición rellenados con JavaScript no se pueden evaluar sin navegador | OBS-06 |

### Ronda 6 — verificación de D-08, efecto de la regla y calidad de las pruebas

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R6-01 | F5, regla operativa (RF 4, 6.6) | formulario real **tras el despliegue**, sin cambiar nada | la versión nueva conserva prueba y devolución | v4 idéntica a v3; cada campo viaja una sola vez | OK (D-08 resuelto) |
| R6-02 | F5, regla operativa | solo `MaxIntentosEntrega` 3 → 2 desde el formulario | cambia únicamente ese valor | v5: intentos 2; prueba, devolución, paradas y entrega parcial intactos | OK |
| R6-03 | F5, efecto en caliente | simulador con máximo 2: 1 intento → queda 1; 2 intentos → `superaMaximoIntentos = true`; luego máximo 3 con 2 intentos | el efecto es inmediato y por versión | confirmado en los tres casos (`versionAplicada` 5 y luego 6) | OK |
| R6-04 | Calidad (6.15) | suite de integración repetida 3, 8 y 7 veces | estable | `ComercioDadoDeBaja_ConUnTokenAbiertoYaNoPuedeOperar` fallaba 1 de cada 3 a 5 corridas (400 al crear el comercio) | **DEFECTO D-09** (de las pruebas) |

### Ronda 7 — escenario F1 (caída de una instancia con tráfico)

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R7-01 | F1, RNF 6.13 | 235 s de tráfico continuo (≈ 3,5 pedidos/s: lecturas del tarifario y de envíos y un alta real cada 10 pedidos) mientras se ejecuta `docker stop` de `api1` (s 30), `docker start` (s 80), `docker stop` de `api2` (s 130) y `docker start` (s 180) | continuidad del servicio, sin errores visibles | **811 pedidos, 0 no-2xx** en las cinco fases (19, 28, 37, 36, 36 pedidos); las 81 altas durante la prueba dieron 201; latencia mediana 58-63 ms en todas las fases; máximo 1.069 ms (reintento de Caddy); las dos APIs terminaron `healthy` | OK |

### Ronda 8 — escenario F4 (dos operadores de configuración distinta)

Preparación: se crearon, con el administrador del operador 2, un despachador, un operario, un comercio y su usuario (`audit.*@example.com`) y la zona `AUDIT Centro Op2` (con el mismo polígono del centro de Montevideo), porque el operador 2 se entrega **sin zonas de cobertura y no puede admitir envíos** (ver OBS-10).

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R8-01 | Multitenencia, lectura (6.5) | 31 recursos por identificador (comercios, cuadros, reglas, zonas, flota, retiros, envíos…): el id sale del listado del operador 1 y se pide con la identidad equivalente del operador 2 (administrador, despachador, operario, comercio) | 403 o 404 | 27 × 404 con control 200 en el operador 1; 4 casos no concluyentes (ids de reglas anidadas que ni el operador 1 puede consultar por ese endpoint) | OK |
| R8-02 | Multitenencia, escritura (6.5) | 15 `PUT`/`PATCH`/`DELETE` sobre ids del operador 1 con la identidad del operador 2 | ninguno 2xx | 15 sin 2xx | OK |
| R8-03 | Multitenencia, listados (6.5) | 37 listados con la misma identidad en cada operador | ningún id en común | 37 sin intersección | OK |
| R8-04 | Configuración distinta (6.5, 6.6) | tarifa, reglas e identidad de cada operador | distintas | op1: 150 / 300 / 120, 3 intentos, plazo estándar 2 días; op2: 180 / 350 / 160, 2 intentos, plazo estándar 3 días; nombre comercial distinto, mismo color (OBS-09) | OK |
| R8-05 | Operación simultánea (F4) | mismo destino y mismo bulto en los dos operadores | cada uno con su tarifa | op1 cotiza 150; op2 cotiza 180 | OK |
| R8-06 | Cambio en caliente aislado (F4, 6.6) | op2: Estandar 180 → 190; se consulta y se da de alta en ambos operadores | op2 cambia al instante; op1 no | op2 190 (v2); op1 150 (v7 sin cambios); restaurado a 180 (v3) | OK |

OBS-09 (baja): el color de marca de los dos operadores demo es el mismo (`#1D4ED8`); para que la diferencia visual se note en la defensa conviene cambiar el del operador 2 desde Identidad Visual.
OBS-10 (media para F4): el operador 2 no trae zonas ni comercios de demostración; para operar con él en la defensa hay que crearle una zona (con polígono), un comercio y un usuario de comercio. Conviene sembrarlos o tener la secuencia ensayada.

### Ronda 9 — escenario F6 (avisos a comercios)

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R9-01 | Receptor de prueba (6.9, F6) | habilitar `RECEPTOR_PRUEBA_AVISOS=true` en la VM y reiniciar `api1` y `api2` por turnos | receptor disponible; el servicio no se corta | las dos APIs `healthy`; `GET /api/testing/webhook-receiver/recibidos` (administrador) → 200; el `POST` anónimo del worker → 200 | OK |
| R9-02 | Suscripción (6.9) | comercio suscribe `EnvioAdmitido` (el primer evento del catálogo que le ofrece la plataforma) hacia el receptor de prueba, da de alta un envío y se espera más de 60 s | aviso firmado entregado al receptor | **ningún aviso**: el worker procesa el evento con `"Avisos": 0`; la tabla `AvisosComercio` no recibe filas | **DEFECTO D-10** |

| R9-03 | Suscripción (6.9, F6), tras desplegar D-10 | mismo alta con suscripción `EnvioAdmitido` | aviso generado y entregado | el aviso **se genera** (historial con firma `sha256=…`, estado `Reintentando`, 2 intentos) pero la entrega falla: "Destino no permitido: la URL del aviso apunta a una dirección interna. (api1:8080)" | **DEFECTO D-11** |
| R9-04 | Reintentos (6.8, 6.9) | primer fallo de entrega del aviso anterior | reintento programado con espera creciente | `Reintentando`, 2 intentos en el primer minuto y `proximoReintento` en el historial | OK |
| R9-05 | F6, entrega normal (6.9), tras desplegar D-10 y D-11 | alta con el receptor sano | aviso entregado con firma válida | `Entregado` en el primer intento; además el aviso que había quedado atascado por D-11 se **recuperó solo** al corregir el worker (3 intentos, HTTP 200) | OK |
| R9-06 | F6, receptor detenido | receptor en modo 503; 3 altas | 3 avisos `Reintentando` con espera creciente | 3 × `Reintentando`, 1 intento, HTTP 503, próximo reintento a +1 min; el primer reintento automático también falló (2 intentos) y el siguiente se reprogramó a +5 min; el receptor registró cada intento con firma válida | OK |
| R9-07 | F6, reactivación | receptor en 200; reintento manual de 2 avisos desde el panel del comercio (`POST /api/comercio/avisos/{id}/reintentar`) y el tercero sin intervención | los tres se recuperan | 2 `Entregado` al instante; el tercero se recuperó solo a los **282 s** (reintento programado de 5 min); los tres con `intentos = 3` | OK |
| R9-08 | F6, idempotencia (6.8) | 11 entregas registradas por el receptor en total | una sola entrega exitosa por aviso | cada aviso con fallas tuvo 2 × 503 y **1 × 2xx**; 0 avisos con más de una entrega exitosa; 11 de 11 firmas válidas | OK |

Política observada: 4 intentos con esperas de 1, 5 y 15 minutos (`ReglasAvisosComercio`). Para la defensa conviene **reintentar a mano** desde el panel en lugar de esperar los 5 minutos.

### Ronda 10 — escenarios F2 y F3 (móvil sin conexión y conflicto de sincronización)

La app MAUI no se puede ejercitar desde esta máquina; se probó el contrato que usa contra producción con una dotación propia (repartidor y vehículo `AUDIT`, dos envíos recibidos y clasificados en depósito, una hoja de ruta). El "sin conexión" se emula así: el repartidor descarga la ruta, confirma la carga y deja de llamar; mientras tanto el servidor cambia; después el dispositivo envía su cola con las mismas claves de idempotencia.

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R10-01 | RF 18, 24 (descarga y sincronización diferida) | descarga de la ruta, confirmación de carga y envío posterior de dos entregas con evidencia (firma, foto, receptor y documento) | 200 y estado `Entregado` | 200 en ambas; `Entregado` / `Entregada`; una prueba de entrega por envío | OK |
| R10-02 | RF 15, 24, ADR-001 (entrega vs retiro de la ruta) | ruta **en tránsito**; el despachador retira el envío A con un motivo interno del catálogo (`RETORNO_SIN_INTENTO`: A pasa a `NoEntregado`, pendiente de rendición) y luego el dispositivo envía la entrega de A | prevalece la entrega física con evidencia y el conflicto queda asentado | 200 `Entregado`; fila en `ConflictosSincronizacion`: `Envio`, servidor `NoEntregado`, dispositivo `Entregado`, política `PrevaleceEntregaFisicaConEvidencia`; eventos del envío `RetiroEnRuta` y `EntregaExitosa` una vez cada uno | OK |
| R10-03 | Idempotencia (6.8, 24) | reenvío idéntico de la cola con las mismas claves (corte de red) | mismo resultado, sin duplicados | 200 con el mismo contenido; un solo evento `EntregaExitosa` y una sola prueba por envío | OK |
| R10-04 | Integridad de la evidencia | entrega de un envío ya entregado con **otra** clave y otro nombre de receptor | no sobrescribe la evidencia ni duplica eventos | 200 (se devuelve el resultado existente); la prueba conserva el receptor original ("Ana Gomez") y sigue habiendo un solo evento | OK (OBS-11) |
| R10-05 | Estado inválido (RF 11) | entrega de un envío que el despachador sacó de una ruta **no despachada** (`EnDeposito`) | rechazo | 400 "no se puede entregar porque su estado actual es 'EnDeposito' (se requiere 'EnTransito')" | OK |
| R10-06 | Validación del receptor (RF 20) | documento del receptor con un solo dígito | rechazo | 400 "El documento del receptor debe contener exactamente 8 dígitos numéricos." (acepta `1.234.567-8` y lo normaliza) | OK |

OBS-11: la segunda entrega de un envío ya entregado responde 200 con el resultado existente en lugar de 409; es coherente con la idempotencia por envío y la evidencia original queda intacta, pero la app no se entera de que su segundo intento no se aplicó.
OBS-12: el catálogo de motivos internos del operador demo tiene un solo código (`RETORNO_SIN_INTENTO`); retirar un envío en la calle sin ese catálogo falla con 400, lo que conviene saber al ensayar el escenario 3.

### Ronda 11 — concurrencia (RF 15, 6.4)

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R11-01 | RF 15 (un envío no puede estar en dos rutas) | 3 rondas; en cada una, dos peticiones **simultáneas** (misma instancia de cliente, enviadas a la vez) asignan el mismo envío a dos rutas distintas con dos dotaciones propias | una ruta creada y la otra rechazada | ronda 1: 400 y 201 ("El envío ya fue asignado a otra hoja de ruta"); rondas 2 y 3: 409 y 201 ("Otra operación simultánea ya registró los mismos datos"); 0 respuestas 5xx | OK (OBS-13) |
| R11-02 | 6.4 concurrencia optimista | editar un comercio con el sello vigente, con el mismo sello ya usado, con el sello nuevo, y dos ediciones **simultáneas** con el mismo sello | 200 / 409 / 200 / una 200 y una 409 | 200 / 409 / 200 / 409 y 200 | OK |

OBS-13: perder la carrera da 400 o 409 según el instante en que la segunda petición lee el estado; ambos rechazan correctamente, pero un cliente que solo reintente ante 409 trataría distinto al 400.

### Ronda 12 — borde, sesión y cuentas

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R12-01 | Exposición de red (6.3, 6.14) | 28 puertos TCP desde Internet (SSH, bases de datos, cola, caché, Prometheus, Grafana, paneles) | solo web | abiertos: **22, 80 y 443**; 5432, 5672, 15672, 6379, 9090, 3000 y el resto, cerrados | OK (OBS-15: SSH abierto a todo Internet) |
| R12-02 | TLS (6.14) | HTTP, TLS 1.1, 1.2 y 1.3 en los 4 dominios; certificado | HTTP→HTTPS; TLS ≥ 1.2; certificado válido | 308 a HTTPS; TLS 1.1 rechazado; 1.2 y 1.3 correctos; Let's Encrypt vigente hasta 11/02/2027 | OK |
| R12-03 | Cabeceras (6.3) | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`, `Strict-Transport-Security`, `Permissions-Policy` | todas, sin cabeceras que revelen versión | todas salvo `Content-Security-Policy` en Backoffice, Portal, Seguimiento y Grafana (la API sí la envía); ninguna cabecera reveladora | OBS (declarada el 13/11) |
| R12-04 | CORS (6.3) | `OPTIONS` con origen ajeno, con el origen del portal y con `null` | solo el portal | ajeno y `null` sin `Allow-Origin`; el portal con su origen exacto y credenciales | OK |
| R12-05 | Grafana (6.12) | sin sesión: `/api/search`, `/api/datasources`, `/api/dashboards/home`, `/api/admin/stats`, `/api/user`; login `admin`/`admin` | 401 | todos 401; `admin/admin` rechazado; `/api/health` y **`/metrics` responden 200 sin sesión** | OBS-16 (baja) |
| R12-06 | Cookies (6.3) | banderas de las cookies del Backoffice y del balanceador | `Secure`, `HttpOnly`, `SameSite` | sesión: `secure; httponly; samesite=lax`; balanceador: `Secure; HttpOnly`; **antifalsificación sin `Secure`** | OBS-14 (ver nota) |
| R12-07 | Seguimiento público (RF 25, 6.3) | JSON de un envío entregado buscando GUID, correo, teléfono, documento, dirección, nombre y evidencia | ninguno | 0 coincidencias; solo código, estado, historial público, avance de bultos y marca del operador | OK (OBS-17) |
| R12-08 | Bloqueo de cuenta (6.3) | 6 contraseñas incorrectas y luego la correcta | bloqueo temporal | 401 ×4, **423 al 5.º intento**, 423 también con la contraseña correcta | OK |
| R12-09 | Enumeración de usuarios (6.3) | 4 intentos con correo existente y 4 con inexistente | mismo mensaje y tiempo | mismo mensaje ("Credenciales inválidas."); **tiempos distintos: ≈ 165-230 ms frente a ≈ 86 ms**, además el bloqueo solo ocurre en cuentas que existen | OBS-18 |
| R12-10 | Política de contraseñas (6.3) | `1`, `12345678`, `password`, `abcdefgh`, `Abcdefg1`, `Abcdefg1!`, `Abcdefghijkl`, `Abcdefgh12!`, `Valida-2026-Xy!` | débiles rechazadas | rechazadas con motivo claro (mínimo 10 caracteres y tres tipos); las dos válidas dan 201 | OK |
| R12-11 | Vigencia del token (6.3) | `exp` − `iat` del JWT | acotada | 28.800 s (8 h) | OK |
| R12-12 | Revocación (6.3) | iniciar sesión, desactivar al usuario con el administrador y usar el token anterior | rechazo inmediato | **200**: el token seguía valiendo hasta 8 h | **DEFECTO D-12** |

Notas de la ronda 12:
- **OBS-14 (cookie antifalsificación sin `Secure`) no se corrigió a propósito.** Se probó forzar `SecurePolicy = Always` y una prueba bajo el entorno Production mostró que ASP.NET lanza `InvalidOperationException` si cree que la petición no es HTTPS, y que la cookie salga sin `Secure` indica que el Backoffice ve las peticiones de Caddy como HTTP. Desplegarlo habría dado **error 500 en todos los formularios del Backoffice**. El cambio se descartó y queda como pendiente de investigar por qué el esquema HTTPS no llega al Backoffice.
- OBS-15: el grupo de red permite SSH desde cualquier origen (`cidr_ssh_permitido = "*"`); el acceso es solo por clave, pero conviene limitarlo a la IP de quien administra.
- OBS-16: `/metrics` de Grafana, sin sesión, expone métricas internas del propio Grafana.
- OBS-17: el historial público muestra pasos internos (por ejemplo "NoEntregado" cuando el despachador retira un envío en ruta y luego se entrega).
- OBS-18: el tiempo de respuesta del login permite distinguir cuentas existentes de inexistentes; se resuelve verificando un hash ficticio cuando el correo no existe.

### Ronda 13 — registros, tareas programadas, outbox, cuerpos grandes y pipeline

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R13-01 | Secretos (6.3) | registros de los 6 contenedores con estado (api1, api2, worker, backoffice, portal-comercio, caddy): contraseñas, tokens `Bearer`/JWT, claves de firma o de API, cabecera `Authorization`, correos | ninguno | 0 líneas con posibles secretos y 0 con correos en los seis | OK |
| R13-02 | Tareas programadas (6.13) | `CierresDiariosOperacion` e `IndicadoresCumplimiento` con dos instancias de API y un worker | una fila por operador y fecha (o por combinación) | cierres: 6 filas, 6 combinaciones; indicadores: 8 filas, 8 combinaciones; sin duplicados | OK |
| R13-03 | Outbox e idempotencia (6.8) | tamaño del outbox y de las claves del consumidor | acotado y todo procesado | 465 mensajes, **todos procesados**, 197 kB de contenido; 232 claves de idempotencia; base completa de 11 MB (el outbox procesado no se purga: OBS ya declarada, sin impacto a esta escala) | OK |
| R13-04 | Cuerpos grandes (6.3) | `POST /api/envios` autenticado con 30 MB y con 120 MB de JSON | rechazo temprano sin agotar recursos | HTTP 413 en 0,7 s y 0,24 s (se corta a los 64 kB enviados); `/ready` sigue en 200 | OK |
| R13-05 | CI/CD (6.14, 6.15) | estado real del pipeline de GitHub Actions y de las imágenes de `ghcr.io` | pipeline en verde con despliegue | **funciona**: los últimos seis `push` terminaron en `success` (compilar y probar, publicar imágenes, desplegar); las imágenes `latest` de `ghcr.io` son de hace una hora y las de la VM coinciden. Esto corrige la nota anterior de que el pipeline estaba bloqueado por facturación | OK (ver OBS-19) |

OBS-19: **cada `push` a `main` despliega solo** (el trabajo `desplegar` del pipeline recrea los contenedores en la VM, unos 10 minutos de compilación y despliegue por `push`), también los commits que solo cambian documentación. Para no reiniciar los servicios en medio de una prueba conviene agrupar los `push` o usar `[skip ci]` en los commits de documentación. Mis `desplegar.ps1` manuales de esta noche eran redundantes pero inofensivos.
OBS-20: la reconstrucción del escenario 7 (`reconstruir.ps1`, con `-DesdeRegistro`) toma las imágenes de `ghcr.io`, que hoy están al día; si el pipeline vuelve a fallar, la VM reconstruida quedaría con imágenes viejas. Conviene comprobar la fecha de la imagen antes de la defensa.

### Ronda 14 — archivos y tiempo real

| ID | Área (letra) | Condiciones probadas | Esperado | Real | Veredicto |
|---|---|---|---|---|---|
| R14-01 | Importación CSV, idempotencia (RF 7) | lote de 3 filas (2 válidas, 1 con peso inválido), el mismo archivo otra vez y un archivo parcial (1 repetida y 1 nueva), contra producción con el geocodificador real | sin duplicados; cada fila informada | 1.ª: 2 creados, 1 con error; 2.ª: `reimportacion = true`, 0 creados, 2 duplicados; parcial: 1 creado, 1 duplicado | OK |
| R14-02 | Importación CSV, contenido hostil (6.3) | fórmula `=HYPERLINK`, XSS y SQL en el destinatario, sin cabecera, columnas de más y de menos, campo de 5.000 caracteres, bytes al azar, 0 bytes, UTF-16, Latin-1 con acentos, nombre de archivo con ruta `..\..\..\x.csv` | sin 5xx, errores claros | 200 con error por fila o 400 con mensaje ("Faltan columnas obligatorias…", "Adjunte un archivo CSV…"); Latin-1 aceptado; ningún 5xx; `/ready` sigue en 200 | OK |
| R14-03 | Exportación de reportes (6.3) | lectura del código de `ReporteCsv` | neutraliza fórmulas en celdas de texto | entrecomilla y antepone `'` a los prefijos `=`, `+`, `-`, `@` | OK |
| R14-04 | Tiempo real, autorización (6.10) | tres conexiones reales al hub `/hubs/operacion` de producción: un **usuario de comercio** del operador 1, un administrador del **operador 2** y el repartidor; el comercio invoca `TransmitirCoordenadas`, `SuscribirAOperador` (operador 2) y `RegistrarRepartidor` (repartidor ajeno) | rechazo; nada llega a otro operador | **todo aceptado**: el comercio transmitió una posición falsa que recibieron el administrador del operador 2 y el repartidor; se suscribió al grupo de otro operador y al canal personal de un repartidor ajeno | **DEFECTO D-13** |

## Defectos y soluciones

### D-13 (alta) — El hub de operación acepta posiciones, grupos y canales de cualquier usuario y los difunde a todos los operadores
- **Dónde:** `src/Api/Hubs/OperacionHub.cs`. La clase solo exigía estar autenticado y sus métodos confiaban en lo que enviaba el cliente: `TransmitirUbicacion` y `TransmitirCoordenadas` (identificador del repartidor, de la ruta, coordenadas), `RegistrarRepartidor(Guid)` y `SuscribirAOperador(string)`. La retransmisión usaba `Clients.Others`, es decir, **todas** las conexiones del hub de cualquier operador. Además todo usuario, también de comercio, se unía al grupo `operador-{id}` al conectarse.
- **Evidencia (producción, tres conexiones reales):** el usuario de comercio del operador 1 invocó `TransmitirCoordenadas` (aceptado) y el evento llegó al administrador del operador 2 y al repartidor; `SuscribirAOperador` con el operador 2 y `RegistrarRepartidor` con un repartidor ajeno también se aceptaron. El servidor envía por el canal `repartidor_{id}` las novedades de ruta de cada repartidor, así que quien se une a ese canal las escucha.
- **Impacto:** violación del aislamiento por inquilino (6.5, 6.10 y regla 3 del proyecto): falsificar la flota en el tablero, leer la posición y las novedades de otros operadores y, para un comercio, ver toda la flota de su operador. La app móvil sí usa este hub, por eso no se pudo quitar.
- **Solución:** `TransmitirUbicacion`, `TransmitirCoordenadas` y `RegistrarRepartidor` exigen la política de repartidor; el operador y el repartidor se toman del token y de la base (no del cuerpo); la ruta enviada solo se acepta si es del propio repartidor; la difusión va solo al grupo del propio operador (`OthersInGroup`); `SuscribirAOperador` solo acepta el operador del token y nunca desde un comercio; `RegistrarRepartidor` solo acepta el propio repartidor; los usuarios de comercio ya no se unen al grupo del operador.
- **Pruebas que fallaban antes** (cliente SignalR real contra la API, 3 de 3 rojas sin el arreglo): `OperacionHubApiTests.UnUsuarioDeComercio_NoPuedeTransmitirPosiciones`, `LaPosicionDeUnRepartidor_SoloLaRecibenLasConexionesDeSuOperador_NoDeComercioNiDeOtroOperador` (con control positivo: el administrador del mismo operador sí la recibe) y `NoSePuedeUnirseAlGrupoDeOtroOperadorNiAlCanalDeOtroRepartidor`.
- **Pendiente tras desplegar:** repetir las tres conexiones en producción y comprobar que la app del repartidor (con su propio token) sigue transmitiendo.

### D-12 (media-alta) — El token de un usuario desactivado sigue valiendo hasta 8 horas
- **Dónde:** el JWT se valida solo por firma y vencimiento; nada comprobaba que el usuario siguiera activo. Para los comercios dados de baja ya existía `ComercioActivoMiddleware`, pero no para el resto de los perfiles (administrador, despachador, operario, repartidor).
- **Evidencia (producción):** token de un despachador, `PATCH /api/usuarios/{id}/estado` con `activo=false` (204) y el mismo token → `GET /api/planificacion/hojas-de-ruta/` → **200**.
- **Impacto:** un empleado despedido o una cuenta comprometida que el administrador desactiva conserva el acceso (incluida la app del repartidor y el tablero) hasta 8 horas. Contradice la intención de "desactivar".
- **Solución:** `UsuarioActivoMiddleware` consulta por clave primaria si el usuario existe y está desactivado y responde 401 con `USUARIO_DESACTIVADO`; los pedidos sin usuario en la base (modo demostración) no se tocan. El inicio de sesión de un usuario desactivado ya devolvía 403.
- **Prueba que fallaba antes:** `UsuarioDesactivadoApiTests.UnTokenAbierto_DejaDeValerCuandoElAdministradorDesactivaAlUsuario` (200 sin el middleware; con él 401, y 200 de nuevo al reactivar).

### D-11 (alta para F6) — El worker no puede entregar avisos al receptor de prueba: la excepción anti-SSRF está en el servicio equivocado
- **Dónde:** `docker-compose.prod.yml`. `Avisos__HostsPermitidos: "api1,api2"` figuraba en las APIs y en el **Backoffice** (que no envía avisos), pero no en el **worker**, que es quien entrega los avisos (`HttpWebhookSender` con `DestinoWebhookSeguro`). Confirmado en el contenedor: `docker exec shippingbox-worker-1 printenv` no tiene ninguna variable `Avisos__*`.
- **Evidencia (producción):** el aviso queda `Reintentando` con "Error de red: Destino no permitido: la URL del aviso apunta a una dirección interna. (api1:8080)"; el receptor no recibe nada.
- **Impacto:** la letra exige un receptor de prueba "con el que demostrar el mecanismo, incluidos los escenarios de falla" y el escenario 6 consiste en detenerlo y reactivarlo. En producción el receptor propio era inalcanzable para el worker: no había entrega que detener ni que recuperar. Las pruebas de código no lo ven porque es configuración del despliegue.
- **Solución:** la excepción se agrega al worker. Una prueba nueva lee el compose y exige que el worker defina `api1` y `api2`, y que el receptor de prueba no se habilite por defecto.
- **Prueba que fallaba antes:** `ComposeDeProduccionTests.ElWorker_PermiteEntregarAvisosAlReceptorDePruebaInterno`.

### D-10 (alta para F6) — La suscripción a "EnvioAdmitido", la primera del catálogo, nunca genera avisos
- **Dónde:** `ProcesarMensajeCommandHandler.CoincideConEvento`. El comercio puede suscribirse a `EnvioAdmitido` ("Admisión de Envío: emitido cuando el paquete es admitido por API, carga masiva o registro manual"; catálogo en `ObtenerConfiguracionAvisosQueryHandler` y lista permitida en `ConfigurarAvisosCommandHandler`). El alta publica el evento con tipo de dominio `AltaEnvio` y estado nuevo `Admitido`; el worker compara la suscripción solo con esos dos valores, y `EnvioAdmitido` no es ninguno.
- **Evidencia (producción):** alta con suscripción `EnvioAdmitido` → outbox procesado con `"Avisos":0`; tabla `AvisosComercio` vacía. Los demás eventos del catálogo sí coinciden con el nombre del estado (`EnDeposito`, `EnTransito`, `Entregado`, …).
- **Impacto:** es el único evento que el propio comercio provoca sin intervención del operador: en el escenario 6 ("generar eventos") quien configure la admisión como evento no recibe nada y el panel de avisos queda vacío.
- **Solución:** la suscripción `EnvioAdmitido` se interpreta como el estado `Admitido` (alias en `NormalizarTipoEventoSuscripto`, que además ahora conserva el resto del tratamiento del prefijo `v1:`/`v2:`).
- **Prueba que fallaba antes:** `EntregaParcialBultosTests.ProcesarMensaje_SuscripcionAEnvioAdmitido_GeneraElAvisoDelAltaDelEnvio` (colección vacía sin el arreglo).

### D-09 (baja, calidad de las pruebas) — Tres pruebas de integración fallaban de forma intermitente por RUT repetidos
- `AutorizacionPorPerfilApiTests` y `ComercioConVariosOperadoresApiTests` (dos pruebas) calculaban el RUT como `(hash + i·k) % 10`: solo hay 10 valores posibles. Con la base local acumulada entre corridas, el alta del comercio daba 400 por RUT duplicado una de cada tres a cinco veces. No dependía del código de producción (se comprobó repitiendo la suite sin los cambios del día).
- **Solución:** 12 dígitos aleatorios. **Verificación:** 12 corridas seguidas de la suite de integración sin fallas. Es la causa de un posible "pipeline rojo sin motivo" antes de la defensa.
- **Nota de proceso:** al publicar D-08 se vio un fallo de esta prueba en la salida y se publicó igualmente antes de investigarlo; se investigó y se corrigió después. Convenio para el resto de la noche: no se publica con una prueba en rojo sin explicación.

### D-08 (crítica para F5) — Publicar una regla operativa cambia en silencio la prueba de entrega y la política de devolución
- **Dónde:** `src/Web/Backoffice/Pages/ReglasOperativas/NuevaVersion.cshtml`. Para "Tipo de prueba de entrega" y "Política de devolución" la página muestra una lista desplegable y una caja de texto con **el mismo nombre de campo**. El navegador envía ambos y ASP.NET toma el primero (la lista). Como el valor vigente real no figura entre las opciones, la lista no tiene nada marcado y envía su primera opción.
- **Evidencia (producción):** formulario enviado tal cual, sin cambios → la versión 2 tenía "Completa" y "Custodia por 72 horas…" en lugar de los valores vigentes. Además, quien eligiera "Otro (texto libre)" guardaría la palabra "personalizado".
- **Impacto:** en el escenario 5 de la defensa ("modificar una regla operativa en caliente") cambiar, por ejemplo, el máximo de intentos modifica a la vez dos reglas que nadie tocó: la app móvil pasaría a exigir foto además de firma y la política de devolución sería otra. Pasó inadvertido porque el mock coincidía justo con la primera opción.
- **Solución:** las listas ya no envían nada (solo rellenan la caja de texto con JavaScript, que es lo que se guarda) y marcan la opción que coincide con el valor vigente (o "Otro" si no coincide). El mock usa los valores reales.
- **Prueba que fallaba antes:** `FormulariosDelBackofficeTests.ReglasOperativasNuevaVersion_SinTocarNada_ConservaLaPruebaDeEntregaYLaPoliticaDeDevolucion` (sin el arreglo el campo llega dos veces).
- **Nota (OBS-07):** el dominio normaliza el texto de la prueba a una categoría al publicar ("Firma", "Completa"…); el valor sembrado en producción ("Firma digital y documento del receptor") no es una categoría y desaparece en la primera versión nueva. Los errores de la API por cuerpo con codificación inválida responden 400 sin texto (OBS-08).

### D-07 (alta para F5) — La pantalla no deja publicar la tarifa vigente y no dice por qué
- **Dónde:** `CrearReglaTarifaDto` (`[Range(0.001, 1000)]` en `VolumenHasta`) y `NuevaVersion.cshtml(.cs)`. La tarifa real usa 1.000.000.000 como "sin límite" de volumen; el formulario precarga ese valor y luego lo rechaza por superar 1.000. Al fallar, la página se vuelve a mostrar sin ningún mensaje (el resumen de validación no se pintaba).
- **Evidencia (producción):** formulario con los valores reales → HTTP 200, sin cambios, sin texto de error; con `VolumenHasta = 1000` → 302 y versión publicada.
- **Impacto:** en el escenario 5 de la defensa el administrador cambia un precio y "no pasa nada". Se suma a D-06: aun corregida la lista de modalidades, publicar fallaba en silencio.
- **Solución:** el rango admite hasta 1.000.000.000 (con mensaje exacto) y la página muestra siempre el motivo ("No se pudo publicar: …"), también para las validaciones propias de la página.
- **Pruebas que fallaban antes:** `TarifaNuevaVersion_ConElVolumenSinLimiteDeLaTarifaReal_SePublica` y `TarifaNuevaVersion_ConUnDatoInvalido_MuestraElMotivoEnLaPagina`.
- **Pendiente tras desplegar:** repetir R5-02 sobre producción sin tocar el volumen.

### D-06 (crítica para el escenario F5) — El formulario "Nueva versión" del cuadro tarifario renombra todas las reglas
- **Dónde:** `src/Web/Backoffice/Pages/Tarifas/NuevaVersion.cshtml`. La página precarga las reglas de la versión vigente (modalidades reales: `Estandar`, `Urgente`, `Retiro`), pero la lista desplegable solo ofrece "Estándar 24-48h", "Express en el Día", "Mismo Día" y "Carga Pesada". Ninguna coincide, no queda ninguna opción marcada y el navegador envía la primera.
- **Evidencia (producción, HTML real con sesión de administrador):** las tres filas precargadas no tienen opción marcada. La correspondencia entre una regla y un envío es por nombre (`ReglaTarifa.AplicaPara`, sin distinguir mayúsculas), así que tras publicar una versión desde la pantalla **ninguna alta** con `Estandar` encuentra tarifa.
- **Impacto:** es exactamente el escenario 5 de la defensa ("modificar una tarifa en caliente y verificar su efecto"): al cambiar un precio desde la interfaz, el siguiente envío del comercio se rechaza. Pasó inadvertido porque los mocks del Backoffice y las pruebas usaban el vocabulario viejo ("Estándar 24-48h").
- **Solución:** `ModalidadesTarifa` define el vocabulario real (`Estandar`, `Urgente`, `Retiro`) con etiquetas legibles; la lista marca la opción vigente sin distinguir mayúsculas y conserva como opción un valor desconocido existente en lugar de reemplazarlo. Se corrige también el HTML de las filas nuevas (JavaScript), las reglas sugeridas, el valor por defecto del contrato y el mock.
- **Prueba que fallaba antes:** `FormulariosDelBackofficeTests.TarifaNuevaVersion_CadaReglaPrecargadaConservaSuModalidadAlGuardar`.
- **Pendiente tras desplegar:** repetir el escenario F5 completo (cambiar una tarifa desde la pantalla y dar de alta un envío) y verificar el efecto.

### D-05 (baja) — Una posición `{}` se guarda en (0, 0)
- `latitud` y `longitud` son `double` no nulos: un objeto vacío queda en (0, 0), frente a la costa de África, y aparece en el tablero. **Solución:** se rechaza una lista con elementos nulos y la posición exacta (0, 0). **Prueba:** `EntradasMalFormadasApiTests.Posiciones_ConUnaPosicionNulaOSinCoordenadas_Da400`.

### D-04 (alta) — `{"reglasTarifa":[{}]}` publica una versión con una regla fabricada con valores por defecto
- `CrearReglaTarifaDto` traía por defecto modalidad "Estándar 24-48h", 0–5 kg, 0,05 m³ y $180, de modo que un objeto vacío parecía una regla completa. Una sola petición vacía reemplazaba la tarifa real del operador (ver incidente).
- **Solución:** la modalidad ya no tiene valor por defecto (el dominio rechaza la modalidad vacía) y los elementos nulos se rechazan. **Prueba:** `EntradasMalFormadasApiTests.CuadroTarifario_ConReglasNulasOVacias_Da400YNoCambiaLaVersionVigente`.
- **No se cambió** el manejador para exigir un vocabulario cerrado ni una regla `Estandar`: las pruebas y flujos existentes publican otros nombres. Publicar una versión sin regla para la modalidad que usan las altas sigue siendo posible (OBS-05).

### D-03 (media) — Elemento nulo en listas de zonas y cuadros: HTTP 500
- `POST` y `PUT /api/zonas-cobertura` (`franjasHorarias:[null]`), `POST /api/zonas-cobertura/validar-franjas` (`[null]`) y `POST /api/cuadros-tarifarios` (`reglasTarifa:[null]`) leían propiedades de un elemento nulo. **Solución:** `ValidacionDeCuerpos.SinNulos` convierte el nulo en 400 con mensaje. **Pruebas:** `Zona_ConUnaFranjaNula_…`, `ValidarFranjas_ConUnaFranjaNula_…` y la del cuadro.

### D-02 (media) — Un elemento nulo en la lista de bultos provoca HTTP 500
- **Dónde:** el mapeo del cuerpo a la orden recorre los bultos con `b.Peso`, `b.Alto`… sin comprobar que `b` exista. Mismo patrón en tres puntos de entrada: `POST /api/envios` (portal, `Program.cs`), `POST /api/integracion/v1/envios` (API por clave, `AltaEnvioCanalesEndpoints.cs`) y `POST /api/comercio/retiros` (`RetirosEndpoints.cs`).
- **Evidencia:** `{"bultos":[null]}` → 500 con `traceId` en producción; sin el arreglo los tres tests nuevos devuelven 500.
- **Impacto:** es el caso de "dato mal formado" que se ve en una defensa: un JSON con un elemento nulo en la lista rompe la petición con un error interno en lugar de explicar el problema. No corrompe datos (la excepción ocurre antes de persistir).
- **Solución:** el elemento nulo se convierte en `DomainValidationException` (400 con mensaje: "Cada bulto debe indicar su peso y sus dimensiones.").
- **Pruebas que fallaban antes:** `CanalesDeAltaApiTests.Api_UnBultoNulo_Da400EnVezDeError500`, `Portal_UnBultoNulo_Da400EnVezDeError500`, `Retiro_UnBultoNulo_Da400EnVezDeError500` (500 sin el arreglo; 400 con el arreglo; las 12 pruebas de la clase pasan).
- **Por qué el primer barrido no lo vio:** enviaba objetos rotos pero nunca un `null` **dentro** de una lista. Se agrega un barrido específico de listas con elementos nulos, vacíos, de otro tipo y anidados (R4-03).

### D-01 (alta) — El límite de tasa es un cupo único compartido por todos los visitantes
- **Dónde:** `src/Api/Program.cs`. Los limitadores de login (10/min), seguimiento público (30/min) e integración sin clave (30/min) se particionan por `RemoteIpAddress`, pero la API no procesaba `X-Forwarded-For` (solo el Backoffice lo hacía). Detrás de Caddy, la API ve siempre la IP del proxy.
- **Evidencia (producción, 14/11 01:3x):** 80 pedidos al seguimiento desde la PC agotaron el cupo; el primer pedido hecho enseguida desde la VM (otra IP) también recibió 429.
- **Impacto:** un solo cliente (o una clase entera probando a la vez en la defensa) deja sin login y sin seguimiento público a todos los demás. Es una denegación de servicio trivial. A la inversa, la protección contra fuerza bruta o enumeración no distingue atacantes.
- **Solución:** `ForwardedHeaders` (`XForwardedFor` y `XForwardedProto`) configurado en la API y `UseForwardedHeaders()` antes de HSTS, HTTPS y el limitador. Es seguro porque los puertos de la API no se publican y Caddy reemplaza el `X-Forwarded-For` del cliente.
- **Prueba que fallaba antes:** `LimiteDeLoginApiTests.ElTopeSeCuentaPorLaIpDelCliente_NoPorLaDelProxy` (sin el arreglo: el segundo cliente recibe 429 en vez de 401; con el arreglo, pasa).
- **Estado:** corregido (`1cbfa3f`), desplegado el 14/11 y verificado en producción desde dos IP distintas (R2-06); la suplantación de `X-Forwarded-For` a través de Caddy no evade el límite (R2-07).
- **Limitación que se mantiene (OBS-02):** el contador es por instancia, así que el tope efectivo por IP es el doble (60/min en seguimiento, 20/min en login) con dos instancias. Ya estaba declarada en el estado de requerimientos; la solución sería un limitador con estado compartido en Redis.

## Hipótesis abiertas
1. **Receptor de prueba apagado en producción (6.9, escenario 6).** `/api/testing/webhook-receiver` da 404; confirmar con qué receptor se demuestra la caída y recuperación de avisos.

## Costos de Azure (evidencia del costo, letra 6.14)

Suscripción *Azure for Students*, región `chilecentral`. Recursos del ambiente (todos creados y gestionados por `infra/terraform-azure`): una VM `Standard_B2als_v2`, un disco de sistema `StandardSSD_LRS` de 64 GB y una IP pública `Standard` estática. **No hay otros recursos que consuman saldo** en la suscripción (verificado con `az vm list`, `az disk list`, `az network public-ip list` y `az group list`; solo existen `rg-ultimamilla-prod` y el grupo gratuito `NetworkWatcherRG`).

Precios de la lista pública de Azure (`prices.azure.com`, consultada el 14/11/2026):

| Recurso | Precio | Por día | Por mes (30 d) |
|---|---|---|---|
| VM `Standard_B2als_v2` (Linux) | 0,0526 USD/h | 1,26 USD | 37,9 USD |
| IP pública estática `Standard` | 0,005 USD/h | 0,12 USD | 3,6 USD |
| Disco `StandardSSD_LRS` E6 (64 GB) | 0,658 USD/mes (más operaciones, despreciables) | 0,02 USD | 0,7 USD |
| **Total** | | **≈ 1,40 USD** | **≈ 42 USD** |

- La VM actual existe desde el 13/11 a las 23:14 UTC (reconstruida con Terraform); el consumo de las VM anteriores (ya eliminadas) no se puede obtener con la CLI en una suscripción de estudiante: hay que mirarlo en *Cost Management* del portal de Azure.
- Hasta la defensa (jueves 19/11 a las 15:00, unas 130 h desde ahora) el ambiente agrega **≈ 7,7 USD**; hasta fin de mes, ≈ 42 USD. Las pruebas de carga y de caída de esta auditoría duraron minutos y no cambian esa cifra de forma apreciable.
- Para ahorrar sin destruir: la VM desasignada (`az vm deallocate`) deja de cobrar el cómputo y mantiene disco e IP (≈ 0,14 USD/día). Después de la defensa conviene `terraform destroy` (ver F7).
- Los precios son de lista; el saldo real descontado puede diferir. El tamaño de la VM (2 vCPU) es la causa de la latencia alta bajo 60 usuarios concurrentes ya anotada el 13/11.

## Limpieza de datos de prueba
(se completa al final)
