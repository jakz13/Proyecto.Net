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

## Defectos y soluciones

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

## Costos de Azure
(se completa durante la auditoría)

## Limpieza de datos de prueba
(se completa al final)
