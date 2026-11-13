# Documento técnico: requerimientos no funcionales

**Proyecto:** Última Milla, plataforma multioperador de distribución de última milla
**Laboratorio .NET 2026 (UTEC)** · Estado al 12/11/2026

---

## Para qué sirve este documento

Un requerimiento funcional dice *qué hace* el sistema (crear un envío, entregar un paquete). Un requerimiento no funcional dice *cómo tiene que comportarse*: que sea seguro, que no se caiga, que aguante muchos usuarios, que se pueda ver qué pasa por dentro.

Este documento cuenta, para cada uno de esos puntos, **qué problema había, qué decidimos, cómo quedó armado y qué limitaciones asumimos**. Cada tema cierra con dónde está la evidencia. Las decisiones más importantes tienen además su registro formal (ADR) en `docs/adr`.

> Nota: los diagramas que exige la entrega se pueden armar a partir de las secciones 1, 4, 5 y 9, que describen los flujos. Están marcados con 📐.

---

## Resumen de un vistazo

| # | Tema | Estado |
|---|---|---|
| 1 | Arquitectura y organización del código | Cumplido |
| 2 | Aplicaciones web (Razor y Blazor) | Cumplido |
| 3 | Seguridad y autenticación | Cumplido, con salvedades declaradas |
| 4 | Base de datos y concurrencia | Cumplido |
| 5 | Multitenancy (varios operadores en un mismo sistema) | Cumplido |
| 6 | Configuración sin tocar código | Cumplido |
| 7 | Caché distribuido | Cumplido |
| 8 | Mensajería y procesamiento en segundo plano | Cumplido |
| 9 | Avisos automáticos a comercios | Cumplido |
| 10 | Tiempo real | Cumplido |
| 11 | Aplicación móvil | **Parcial** (notificaciones con la app cerrada) |
| 12 | Observabilidad | Cumplido, con una salvedad |
| 13 | Escalabilidad y balanceo | Cumplido |
| 14 | Despliegue e infraestructura como código | **Parcial** |
| 15 | Calidad y pruebas | Cumplido |

El detalle fino y actualizado de qué está hecho y qué falta vive en [Estado_de_Requerimientos.md](Estado_de_Requerimientos.md).

---

## 1. Arquitectura y organización del código

**El problema.** Un sistema de este tamaño crece durante meses y lo toca mucha gente. Si las reglas del negocio quedan mezcladas con la base de datos o con las pantallas, cada cambio rompe algo en otro lado.

**Qué decidimos.** Usar *arquitectura limpia*: el sistema se divide en capas concéntricas y las dependencias solo apuntan hacia adentro.

- **Dominio** (el centro): las reglas del negocio, por ejemplo qué estados puede tener un envío y cómo pasa de uno a otro. No conoce a nadie más: ni la base de datos, ni la web.
- **Aplicación**: los casos de uso (crear un envío, planificar una ruta). Solo conoce al dominio.
- **Infraestructura**: todo lo que toca el mundo exterior: la base de datos, la cola de mensajes, el caché.
- **Presentación**: la API, el backoffice, los portales y la app móvil.

Por encima de eso, el sistema se despliega como un **monolito modular** (una API que contiene todos los módulos) más un **worker**, un proceso aparte que trabaja en segundo plano y que solo se comunica con la API a través de una cola de mensajes.

**Cómo nos aseguramos de que se cumpla.** No depende de la buena voluntad: hay una prueba automática de arquitectura (`ReglasDeArquitecturaTests`, con NetArchTest) que corre en cada ejecución del pipeline y **falla si alguien hace que el dominio dependa de la infraestructura**. Además, los cambios de estado de envíos, bultos y retiros pasan por *máquinas de estados declarativas*: no se puede saltar de "pendiente" a "entregado" sin pasar por los pasos válidos.

**Qué aceptamos a cambio.** Hay algo más de código repetitivo (transformar datos entre capas). Es el precio del desacople.

📐 *Diagrama sugerido:* anillos concéntricos Dominio → Aplicación → Infraestructura → Presentación, más el Worker separado conectado por la cola.

**Evidencia:** ADR 01 en el Documento de Análisis y Diseño, `tests/UltimaMilla.Architecture.Tests`.

---

## 2. Aplicaciones web

**El problema.** Hay tres públicos distintos: el personal del operador (backoffice), los comercios (portal) y cualquier persona que quiera seguir un paquete (seguimiento público, el de mayor tráfico y sin login).

**Qué decidimos.**
- **Backoffice:** ASP.NET Core Razor Pages, páginas armadas en el servidor.
- **Portal del comercio y seguimiento público:** Blazor en modo **WebAssembly**, es decir, la aplicación se descarga y corre en el navegador del usuario.

**Por qué WebAssembly.** Con Blazor Server, el servidor tendría que mantener una conexión abierta y memoria reservada por cada persona que mira su paquete. Con mucho tráfico se saturaría rápido, y además repartir esas conexiones entre varias instancias exige configuraciones extra. Con WebAssembly el servidor solo entrega archivos y la API responde sin recordar nada del usuario: cualquier instancia puede atender cualquier pedido.

**Qué aceptamos a cambio.** La primera carga es más lenta (el navegador descarga el entorno de .NET). Después la navegación es casi instantánea.

**Evidencia:** ADR 02 en el Documento de Análisis y Diseño.

---

## 3. Seguridad y autenticación

La seguridad se pensó en capas: quién eres, qué puedes hacer, qué datos puedes ver y qué tan difícil es atacarte.

### 3.1 Quién eres (autenticación)
Los usuarios inician sesión y la API les entrega un **token firmado** (JWT) con su perfil y a qué operador o comercio pertenecen. Las contraseñas se guardan con un algoritmo de protección estándar (PBKDF2 con sal), nunca en texto plano. Tras varios intentos fallidos la cuenta se bloquea un tiempo, y el mensaje de error es siempre genérico para no revelar si el usuario existe.

> **Decisión que declaramos con honestidad:** la letra pide OpenID Connect o ASP.NET Identity como almacén de usuarios. Nosotros hicimos un sistema propio (tabla de usuarios + token propio) usando de Identity solo el componente de contraseñas. Los motivos: nuestro modelo necesita perfiles, operador y vínculo comercio-operador que Identity no resolvía sin extenderlo igual, y un proveedor externo agregaba dependencia y costo. El costo es mantener nosotros la emisión y validación del token. Está documentado en ADR-006.

### 3.2 Qué puedes hacer (autorización por perfil)
Cada acción de la API exige un perfil: administrador, despachador, operario de depósito, repartidor o comercio. La regla es **denegado por defecto**: un endpoint sin política asignada se considera un defecto. El administrador solo configura; no planifica ni trabaja en el depósito.

### 3.3 Qué datos puedes ver (aislamiento)
Ver sección 5.

### 3.4 Qué tan difícil es atacarte (endurecimiento)
- **Secretos fuera del repositorio:** las claves viven en un archivo del servidor o en los secretos de GitHub. En producción, si falta la clave de firma, la aplicación directamente no arranca.
- **HTTPS** con certificado automático, **cabeceras de seguridad**, **CORS restringido** a los dominios propios.
- **Límite de intentos:** por IP para el login (10 por minuto) y para el seguimiento público.
- **Seguimiento público anti-enumeración:** un código válido y uno inexistente responden igual, para que nadie pueda "adivinar" envíos ajenos.
- **Avisos a comercios protegidos (SSRF):** como el comercio elige a qué dirección le avisamos, el sistema rechaza direcciones internas para que nadie use esa función para llegar a nuestra red.
- **Archivos subidos:** los logos solo se aceptan en PNG, JPG o WEBP, hasta 2 MB, verificando el contenido real del archivo.
- **Cifrado en el teléfono:** los datos locales de la app móvil se guardan en una base cifrada.

### 3.5 Las auditorías
Se hicieron dos auditorías, la segunda "como atacante". Encontraron, entre otras cosas, un fallo crítico (cuentas de demo con contraseñas públicas en el repositorio que se creaban en producción) y varios casos de código malicioso inyectable en pantallas. **Todo se corrigió y cada corrección tiene una prueba que antes fallaba.**

### 3.6 Riesgos que aceptamos y decimos de antemano
1. El límite de intentos se cuenta por instancia: con dos instancias el límite real es el doble.
2. Backoffice y portales no tienen todavía una política CSP estricta (la API sí). Queda como mejora porque exige probar cada pantalla en el navegador.
3. El token propio podría migrarse a la librería estándar de Microsoft; no cambia las reglas de autorización.

**Evidencia:** `docs/Seguridad.md` (lista completa), ADR-006, `AutorizacionPorPerfilApiTests`, `SeguridadEndurecimientoTests`.

---

## 4. Base de datos y concurrencia

**La base.** PostgreSQL, administrada con Entity Framework Core. **Cada cambio del modelo lleva su migración** (un archivo versionado que describe el cambio), de modo que la base se reconstruye idéntica en cualquier ambiente. Al arrancar, la primera instancia aplica las migraciones y la segunda espera a que termine, para no migrar las dos a la vez.

**Una regla importante:** el sistema corre en dos instancias y el worker es otro proceso, así que **nada importante puede vivir en la memoria de un proceso**. El estado vive en la base.

**Concurrencia optimista.** Imaginemos que dos despachadores abren el mismo envío y ambos lo modifican. Sin protección, el segundo en guardar pisaría el cambio del primero sin darse cuenta. Con concurrencia optimista, cada registro lleva una marca de versión: si al guardar la versión ya cambió, el sistema avisa del conflicto en lugar de sobrescribir. Está aplicada en las entidades que pueden modificarse a la vez (envíos, retiros, hojas de ruta) y demostrada con pruebas.

**En el teléfono.** La app usa SQLite local cifrado para trabajar sin conexión (ver sección 11).

**Evidencia:** carpeta `Persistence/Migrations`, `ConcurrenciaYAislamientoApiTests`.

---

## 5. Multitenancy: varios operadores en un mismo sistema

**El problema.** Un solo sistema atiende a varios operadores logísticos que no deben verse entre sí. Y dentro de cada operador hay comercios que tampoco deben ver los envíos de otros comercios. Son **dos niveles de aislamiento**.

**Qué decidimos.** Aislamiento **por fila**: todas las tablas del negocio llevan el identificador del operador y los datos de todos conviven en una misma base.

- **Nivel 1, operador:** un filtro global de Entity Framework agrega automáticamente "solo los datos de mi operador" a *toda* consulta. Un programador no puede olvidarse de ponerlo, porque no lo escribe: ya está. Además, al guardar, el sistema impide escribir datos de otro operador.
- **Nivel 2, comercio:** un comercio puede trabajar con más de un operador, por eso existe un vínculo comercio-operador (`ComercioOperador`) que guarda su clave de API, cuenta corriente y liquidaciones. Los casos de uso filtran por ese vínculo. Si un comercio pide un envío ajeno recibe "no existe" (404), no "prohibido": así ni siquiera se entera de que existe.
- **Cómo se sabe quién es el inquilino:** sale del token de la sesión, en cada pedido.
- **Identidad visual y reglas por operador:** cada operador ve su propio nombre, logo y colores, y tiene sus propias reglas y tarifas.

**Por qué no una base o un esquema por operador.** Una base por operador multiplica el costo y el trabajo de operar; un esquema por operador complica las migraciones y el pipeline automático. Para este tamaño y esta infraestructura, por fila es lo razonable.

**Qué aceptamos a cambio.** Toda entidad nueva debe incorporar su filtro y su prueba de aislamiento; si se olvida, el aislamiento falla. Por eso hay pruebas que cruzan operadores y comercios intentando "espiarse".

**Evidencia:** sección de multitenancy del Documento de Análisis y Diseño, ADR-006, pruebas de filtración entre operadores y entre comercios.

---

## 6. Configuración sin tocar código

**El problema.** El cuadro tarifario y las reglas operativas cambian con el negocio. No puede ser necesario programar y desplegar cada vez que cambia un precio.

**Qué decidimos.** Todo eso se edita desde la pantalla de administración y rige al instante. Además está **versionado**: al publicar un cambio no se modifica la versión anterior, se crea una nueva. Un envío ya procesado conserva la tarifa con la que se calculó, aunque después cambie el cuadro. Las versiones publicadas son inmutables.

**Evidencia:** entidad `CuadroTarifario` (con `Version`, `VigenteDesde` y `VigenteHasta`), pruebas de cuadro tarifario y reglas operativas.

---

## 7. Caché distribuido

**El problema.** Algunas consultas se repiten muchísimo y casi siempre devuelven lo mismo. Consultar la base cada vez es un desperdicio.

**Por qué distribuido.** Con dos instancias de la API, un caché propio de cada proceso tendría un defecto grave: si una instancia cambia una tarifa, la otra seguiría mostrando la vieja hasta que expire. Por eso el caché es **Redis**, compartido por ambas.

**Dos casos con perfiles opuestos:**

| Caso | Cómo se usa | Cuánto dura | Cuándo se borra |
|---|---|---|---|
| Seguimiento público | Se consulta muchísimo, los datos cambian varias veces al día | 5 minutos | Apenas cambia el envío |
| Cuadro tarifario vigente | Se consulta mucho, casi nunca cambia | 30 minutos | Al publicar una nueva versión |

**Detalles que importan:**
- Las claves incluyen al operador, así que el caché **no puede mezclar datos entre inquilinos**.
- **Si Redis se cae, el sistema sigue funcionando**, solo que más lento: consulta directo a la base.
- Se mide cuántos aciertos y fallos tiene cada caso de uso y se ve en el tablero técnico.

**Qué aceptamos.** Si fallara la invalidación del seguimiento, el dato podría verse hasta 5 minutos desactualizado. Es aceptable: es información no crítica y se corrige sola.

**Evidencia:** ADR-003.

---

## 8. Mensajería y procesamiento en segundo plano

**El problema.** Cuando un envío cambia de estado hay que avisar al destinatario, notificar al comercio y actualizar indicadores. Hacerlo todo dentro del pedido lo haría lento y frágil. Pero si lo mandamos "aparte", aparecen dos riesgos: guardar el cambio y que el aviso se pierda, o avisar de un cambio que finalmente no se guardó.

**Qué decidimos: el patrón outbox.** La idea es sencilla: el cambio y el evento que lo anuncia se guardan **juntos, en la misma operación de base de datos**. O se guardan los dos o no se guarda ninguno. Después, un proceso independiente toma los eventos pendientes y los publica en la cola (RabbitMQ). Si la cola está caída, el evento espera en la base; no se pierde.

Es automático: la unidad de trabajo agrega el evento sola, así que un caso de uso nuevo no puede olvidarse de publicar.

**Qué pasa si algo sale mal:**
- **Mensaje duplicado:** puede pasar (la entrega es "al menos una vez"). El worker anota cada mensaje procesado, de modo que si llega repetido lo reconoce y no repite avisos ni notificaciones. Es lo que llamamos *idempotencia*.
- **Falla al procesar:** se reintenta esperando cada vez más tiempo. Si se agota, el mensaje va a una **cola de fallidos** que el administrador puede ver y reintentar desde el backoffice.

**Qué aceptamos.** Hay unos segundos de demora entre el cambio y el aviso. Y el outbox crece: todavía no se purga lo ya procesado (con el volumen actual no es un problema).

📐 *Diagrama sugerido:* API → (misma transacción) tabla de cambios + tabla outbox → publicador → RabbitMQ → worker → destinatario/comercio; con la rama de reintentos y cola de fallidos.

**Evidencia:** ADR-004, pruebas de resiliencia de RabbitMQ (con la cola detenida se hicieron altas sin pérdida; al volver, los 6 eventos pendientes se procesaron en 15 segundos).

---

## 9. Avisos automáticos a los comercios

**Qué es.** Los comercios pueden recibir un aviso en su propio sistema cada vez que cambia el estado de un envío.

**Cómo funciona:**
- Cada comercio elige **a qué tipos de evento se suscribe**.
- Cada aviso va **firmado** (HMAC): el receptor puede comprobar que realmente viene de nosotros y no fue alterado.
- Si el sistema del comercio no responde, se **reintenta con esperas crecientes**; si se agotan, pasa a **fallidos**.
- En el portal, el comercio ve el **historial de entregas** y puede **reenviar** las que fallaron.
- Existe un **receptor de prueba** para demostrar el mecanismo, incluidos los escenarios de falla. Está apagado en el servidor salvo que se habilite para una demostración.

**Decisión de diseño: contrato versionado.** Los comercios que ya integraron el formato original (`v1`) no deben romperse por funcionalidades nuevas. Por eso el `v2` agrega el detalle por bulto (entrega parcial) y es el único que recibe los eventos de retiro. Cada comercio elige su versión.

**Evidencia:** ADR-004, `docs/API_Publica.md`.

---

## 10. Comunicación en tiempo real

El tablero de operación se actualiza solo, sin recargar, con **SignalR**. Dos cuidados:
- Respeta la **misma autenticación y el mismo aislamiento** que el resto: un operador solo recibe lo suyo.
- Funciona con **dos instancias de la API**. Si una persona está conectada a la instancia 1 y el cambio ocurre en la 2, igual se entera, porque las instancias se comunican a través de Redis (el "backplane").

**Evidencia:** ADR-003 (Redis como respaldo de SignalR).

---

## 11. Aplicación móvil (.NET MAUI)

La app del repartidor sigue el patrón MVVM y cubre: seguimiento de posición en segundo plano, escaneo de códigos, prueba de entrega (firma, foto, posición y hora), biometría para operaciones sensibles y trabajo sin conexión.

### Trabajar sin señal
Los repartidores pasan por zonas sin cobertura, así que la app funciona **primero sin conexión**: todo lo que hace se guarda en una base local cifrada y se agrega a una cola. Cuando vuelve la señal, la cola se envía en orden.

Cada operación lleva un identificador único, de modo que si se reintenta por un corte de red el servidor la reconoce y no la duplica.

### Cuando el teléfono y el servidor no coinciden
Mientras el repartidor estaba sin señal, un despachador pudo haber reasignado el envío. Las reglas para resolverlo:

| Situación | Qué prevalece | Por qué |
|---|---|---|
| El repartidor entregó con evidencia, pero el despachador había reasignado | **La entrega real** | Un paquete entregado con firma y foto no puede volver a figurar como pendiente |
| El repartidor registró un intento fallido, pero el despachador había reasignado | **El servidor** | La decisión planificada es más reciente que un intento fallido |
| Orden de paradas modificado en ambos lados | **Lo más reciente** | No hay razón para preferir a uno |
| Posiciones GPS acumuladas | Se ordenan por la hora del teléfono | Se conserva el recorrido real |

Todo conflicto queda registrado de forma permanente y visible para los supervisores. Los conflictos de negocio **no bloquean** la cola: se resuelven y se sigue con lo siguiente.

### ⚠ Punto débil: notificaciones con la app cerrada
La letra pide recibir avisos urgentes con la app cerrada. Descartamos Firebase (FCM) porque obliga a depender de una cuenta externa de Google y a manejar credenciales que no deben estar en el repositorio. En su lugar, durante la jornada la app mantiene un servicio en primer plano con una conexión SignalR. Funciona bien con la jornada activa, **pero si el repartidor cierra la app a la fuerza, no recibe avisos hasta volver a abrirla** (las reasignaciones igual se reflejan al reabrir o sincronizar). Lo reconocemos como una cobertura parcial de este requerimiento y un riesgo de calificación.

**Evidencia:** ADR-001 y ADR-002.

---

## 12. Observabilidad

**El problema.** Cuando algo falla a las 3 de la mañana hay que poder responder: ¿qué pasó, dónde y por qué?

**Qué instalamos:**
- **Registros estructurados (Serilog):** cada línea de log lleva un identificador de correlación y los datos sensibles van enmascarados.
- **Identificador de correlación:** cada operación recibe un código (`X-Correlation-Id`) que viaja por la API, queda guardado en el evento del outbox y es recuperado por el worker. Así se puede seguir una operación de punta a punta con un solo código. Hay un script que lo demuestra (`scripts/demo-correlacion.ps1`).
- **Métricas (OpenTelemetry) + Prometheus + Grafana:** el tablero técnico "Última Milla – Técnico" muestra tiempo medio de respuesta, percentiles 95 y 99, tasa de errores, profundidad de la cola, sincronizaciones pendientes, avisos fallidos, aciertos del caché y cuántas instancias están activas.
- **Las métricas no son públicas:** el balanceador no las expone; solo Prometheus las lee, por la red interna.

**Qué aceptamos.** La retención es de 24 horas porque el servidor tiene poca memoria. El tablero está definido por archivos del repositorio, así que se reconstruye con el entorno.

**Salvedad:** la app móvil todavía no envía el identificador de correlación, por lo que hoy el seguimiento arranca en la API y no en el teléfono.

**Evidencia:** ADR-008, `docs/UC-44_Observabilidad_Tablero_Tecnico.md`.

---

## 13. Escalabilidad y balanceo de carga

**El objetivo.** Que si una instancia de la API se cae, el servicio continúe.

**Cómo está armado.** Dos instancias de la API (`api1` y `api2`) detrás de **Caddy**, que reparte el tráfico, entrega HTTPS automáticamente y consulta cada pocos segundos si cada instancia está sana (`/ready`). Si una falla varias veces seguidas, la saca del reparto hasta que se recupere. Se tolera algún fallo aislado antes de retirarla porque el servidor tiene poca CPU y puede tener un mal momento sin estar realmente caído.

**Lo que hace posible esto: una API sin estado.** Nada importante vive en la memoria de la instancia: las sesiones son tokens, el caché y el tiempo real están en Redis, los datos en Postgres.

**Otros puntos exigidos:**
- Puntos de salud `/health`, `/ready` y `/live`.
- **Apagado controlado:** una instancia termina lo que está haciendo antes de cerrarse (40 segundos de margen).
- **Reinicio escalonado:** para actualizar, se reinicia una instancia, se espera que esté sana, y recién entonces la otra. El servicio no se corta.
- **Tareas programadas sin duplicar:** por ejemplo el cierre diario tiene un índice único por operador y día, de modo que aunque dos instancias lo intenten, solo se hace una vez.
- **Claves de protección compartidas** para las cookies del backoffice (guardadas en un volumen).

**Qué aceptamos.** Hay una sola máquina: la alta disponibilidad es entre procesos, no entre servidores. Si se pierde la VM, el plan es reconstruirla con la infraestructura como código.

📐 *Diagrama sugerido:* internet → Caddy (HTTPS) → api1 / api2 → Postgres, Redis, RabbitMQ; worker a un costado; Prometheus y Grafana en la red interna.

**Evidencia:** ADR-007, `docker-compose.prod.yml`, pruebas de resiliencia.

---

## 14. Despliegue e infraestructura como código

**Dónde corre.** En un servidor real de nube pública, con HTTPS válido. Nunca con túneles ni desde la computadora de un integrante. El proyecto empezó en Oracle Cloud (nivel gratuito, VM de 1 GB de RAM) y el 11/11/2026 el despliegue migró a **Azure**.

**Todo en contenedores.** Todo el entorno se levanta con Docker Compose: API (dos instancias), worker, backoffice, portal, seguimiento, base de datos, Redis, RabbitMQ, Prometheus, Grafana y Caddy. A cada contenedor se le fija un tope de memoria para que ninguno pueda ahogar al resto. Solo Caddy publica puertos al exterior (80 y 443); todo lo demás queda en la red interna.

**Infraestructura como código.** El servidor, la red y las reglas de acceso (solo los puertos 22, 80 y 443) se definen con **Terraform**, con variables por ambiente y sin secretos en el código. Se puede planificar, aplicar y destruir. Quedó el código de Oracle como histórico.

**Pipeline (GitHub Actions).** En cada cambio compila, corre todas las pruebas, mide la cobertura y audita los paquetes en busca de vulnerabilidades. Con un push a `main`, despliega solo por SSH. También existe el despliegue manual con `scripts\desplegar.bat`.

**Secretos.** Viven en un archivo `.env.prod` en el servidor o en los secretos de GitHub, nunca en el repositorio. Esta es una de las reglas que no se negocian.

**Costo.** Se trabajó dentro del nivel gratuito; el README de Terraform estima el costo del ambiente de Azure.

**Estado: parcial.** El código de Terraform para Azure está validado y con un `plan` real de 8 recursos, pero **no se aplicó** (para no duplicar el ambiente en funcionamiento). Falta ensayar el ciclo completo de destruir y reconstruir. Además, el pipeline de despliegue automático está hoy bloqueado por un problema con la cuenta de GitHub.

**Evidencia:** ADR-007, `infra/terraform-azure`, `docker-compose.prod.yml`, `.github/workflows/ci.yml`.

---

## 15. Calidad y pruebas

El proyecto incluye **más de 1.000 pruebas automáticas de backend y 111 de frontend**, en cuatro familias:

- **Dominio:** las reglas de negocio puras, sin base de datos.
- **Aplicación e integración:** flujos completos contra una base real, iniciando sesión con usuarios de cada perfil.
- **Arquitectura:** verifica que las capas no se mezclen.
- **Aislamiento:** intentan filtrar datos entre operadores y entre comercios.

La cobertura medida es del **76 %** y la publica el pipeline. Una corrección de seguridad siempre lleva una prueba que fallaba antes de corregirla.

### Pruebas de carga
Con k6 se definieron objetivos de servicio que hacen fallar la prueba si no se cumplen. Resultados medidos contra el ambiente remoto en la consulta pública, la más exigida: **hasta 100 usuarios simultáneos, 179 pedidos por segundo, p95 de 44,7 ms y cero errores de servidor** (el objetivo era p95 < 300 ms). Queda pendiente medir la sincronización simultánea sobre el ambiente remoto.

### Pruebas de resiliencia
Se interrumpieron Redis, RabbitMQ y Postgres con tráfico real y se contrastó lo esperado con lo observado. Dos defectos encontrados se corrigieron. Resumen: sin Redis el sistema sigue funcionando más lento; sin RabbitMQ no se pierden eventos; al volver el servicio todo se normaliza solo.

**Evidencia:** `docs/Plan_de_Carga.md`, `docs/Pruebas_de_Resiliencia.md`, `docs/Plan_de_Pruebas.md`.

---

## Decisiones que conviene tener a mano en la defensa

Son las que más preguntas pueden generar, con la respuesta corta:

| Pregunta probable | Respuesta corta |
|---|---|
| ¿Por qué no usaron OIDC ni Identity completo? | Nuestro modelo necesita perfiles y vínculo comercio-operador; un proveedor externo sumaba dependencia y costo. Está declarado y es un riesgo asumido (ADR-006). |
| ¿Por qué no usaron Firebase para las notificaciones? | Obliga a credenciales externas fuera del repositorio. Usamos un servicio en primer plano con SignalR; no cubre la app cerrada del todo (ADR-002). |
| ¿Cómo evitan perder un evento? | Outbox: el cambio y el evento se guardan en la misma transacción (ADR-004). |
| ¿Y si llega dos veces? | El consumidor registra cada mensaje procesado y reconoce el duplicado. |
| ¿Cómo garantizan que un operador no vea datos de otro? | Filtro global automático en cada consulta + validación al guardar + pruebas que intentan cruzarse. |
| ¿Qué pasa si se cae una instancia? | Caddy la saca del reparto y la otra sigue; nada vive en memoria. |
| ¿Y si se cae Redis? | Sigue funcionando más lento. Medido en las pruebas de resiliencia. |
| ¿Por qué una sola VM? | Recursos del nivel gratuito; la alta disponibilidad es entre procesos y el plan ante pérdida es reconstruir con Terraform. |

## Limitaciones declaradas

Mejor decirlas nosotros antes de que las encuentren:

1. Notificaciones con la app móvil cerrada: cobertura parcial.
2. Terraform de Azure validado pero sin aplicar; falta ensayar destruir y reconstruir.
3. Pipeline de despliegue bloqueado por la cuenta de GitHub.
4. La app móvil no envía aún el identificador de correlación.
5. El límite de intentos se cuenta por instancia (el real es el doble con dos).
6. No hay una política CSP estricta en backoffice y portales.
7. El outbox procesado no se purga.
8. Una sola máquina: sin alta disponibilidad entre servidores.

## Dónde está cada documento

| Tema | Archivo |
|---|---|
| Estado actual de requerimientos | [Estado_de_Requerimientos.md](Estado_de_Requerimientos.md) |
| Seguridad (auditorías y riesgos) | [Seguridad.md](Seguridad.md) |
| Decisiones de arquitectura | [adr/README.md](adr/README.md) |
| Pruebas de carga | [Plan_de_Carga.md](Plan_de_Carga.md) |
| Pruebas de resiliencia | [Pruebas_de_Resiliencia.md](Pruebas_de_Resiliencia.md) |
| Observabilidad | [UC-44_Observabilidad_Tablero_Tecnico.md](UC-44_Observabilidad_Tablero_Tecnico.md) |
| API pública para comercios | [API_Publica.md](API_Publica.md) |
