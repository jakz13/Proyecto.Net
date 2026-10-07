# **Documento de Análisis y Diseño**

## Anthony Curbelo \- Felipe Bernardi \- Francisco Lema \- Joaquin Bassini

**Proyecto .NET 2026**

[**Estrategia de Multitenancy y Aislamiento	3**](#estrategia-de-multitenancy-y-aislamiento)

[**1\. Contexto y Restricciones	3**](#1.-contexto-y-restricciones)

[**2\. Decisión Adoptada	3**](#2.-decisión-adoptada)

[**3\. Resolución del Inquilino en Tiempo de Ejecución	3**](#3.-resolución-del-inquilino-en-tiempo-de-ejecución)

[**4\. Alternativas Consideradas y Motivo de su Descarte	4**](#4.-alternativas-consideradas-y-motivo-de-su-descarte)

[**5\. Justificación y Consecuencias	4**](#5.-justificación-y-consecuencias)

[**Propuesta de Stack Tecnológico y Entorno de Desarrollo	5**](#propuesta-de-stack-tecnológico-y-entorno-de-desarrollo)

[1 Entorno de Desarrollo Local	5](#1-entorno-de-desarrollo-local)

[2 Plataforma y Capa de Presentación	5](#2-plataforma-y-capa-de-presentación)

[3 Persistencia de Datos	5](#3-persistencia-de-datos)

[4 Mensajería Asíncrona y Tiempo Real	5](#4-mensajería-asíncrona-y-tiempo-real)

[5 Seguridad	6](#5-seguridad)

[6 Observabilidad	6](#6-observabilidad)

[7 Despliegue e Infraestructura Cloud	6](#7-despliegue-e-infraestructura-cloud)

[**ADR 01 \- Estilo Arquitectónico Interno y Organización del Código	7**](#adr-01---estilo-arquitectónico-interno-y-organización-del-código)

[1\. Contexto y Restricciones	7](#1.-contexto-y-restricciones-1)

[2\. Decisión Adoptada	7](#2.-decisión-adoptada-1)

[3\. Alternativas Consideradas y Motivo de su Descarte	7](#3.-alternativas-consideradas-y-motivo-de-su-descarte)

[4\. Consecuencias	8](#4.-consecuencias)

[**ADR 02 \- Modo de Renderizado para las Aplicaciones Blazor	9**](#adr-02---modo-de-renderizado-para-las-aplicaciones-blazor)

[1\. Contexto y Restricciones	9](#1.-contexto-y-restricciones-2)

[2\. Decisión Adoptada	9](#2.-decisión-adoptada-2)

[3\. Análisis y Justificación	9](#3.-análisis-y-justificación)

[4\. Alternativas Consideradas y Motivo de su Descarte	10](#4.-alternativas-consideradas-y-motivo-de-su-descarte-1)

[**Plan de Trabajo y Registros de Decisión de Arquitectura	11**](#plan-de-trabajo-y-registros-de-decisión-de-arquitectura)

[1\. Plan de trabajo	11](#1.-plan-de-trabajo)

[1.1 Integrantes y áreas de responsabilidad	11](#1.1-integrantes-y-áreas-de-responsabilidad)

[1.2 Responsabilidades detalladas por integrante	11](#1.2-responsabilidades-detalladas-por-integrante)

[1.3 Cronograma de trabajo por hito	11](#1.3-cronograma-de-trabajo-por-hito)

[1.4 Registro de horas	12](#1.4-registro-de-horas)

# Estrategia de Multitenancy y Aislamiento  {#estrategia-de-multitenancy-y-aislamiento}

## 1\. Contexto y Restricciones {#1.-contexto-y-restricciones}

La plataforma debe dar servicio a múltiples operadores logísticos (inquilinos principales) desde un único despliegue. Cada operador debe tener su operación aislada (zonas, tarifas, usuarios, vehículos). A su vez, existe un requerimiento de un segundo nivel de aislamiento: los comercios (clientes de los operadores) solo pueden acceder a sus propios envíos e información financiera, teniendo en cuenta que un mismo comercio puede trabajar con más de un operador logístico simultáneamente.

La solución debe implementarse utilizando Entity Framework Core en .NET 10 y debe soportar pruebas automatizadas que garanticen que no hay filtración de datos.

## 2\. Decisión Adoptada {#2.-decisión-adoptada}

Se ha decidido implementar un enfoque de Multitenancy por fila (Row-level Isolation) mediante un modelo de multitenancy explícito en el dominio.

Esto significa que:

1. **Aislamiento Primario (Operador):** Toda entidad vinculada de manera directa o indirecta a la operación diaria (como ZonaCobertura, Vehiculo, HojaDeRuta, ReglaOperativa, etc.) incluirá explícitamente el atributo operadorId.  
2. **Aislamiento Secundario (Comercio):** Se implementa mediante la clase de asociación ComercioOperador. En lugar de vincular el Comercio directamente al Operador, esta entidad intermedia manejará la clave de API, la cuenta corriente y las liquidaciones correspondientes al vínculo comercial específico entre ese comercio y ese operador.

## 3\. Resolución del Inquilino en Tiempo de Ejecución {#3.-resolución-del-inquilino-en-tiempo-de-ejecución}

La resolución del inquilino (Tenant Resolution) se realiza a nivel de middleware, a partir de un token JWT emitido directamente por ASP.NET Identity (sin intermediarios externos).

* Cuando un usuario inicia sesión (sea empleado del operador o usuario del comercio), ASP.NET Identity emite un token JWT que incluye los *claims* correspondientes a su operadorId y/o comercioId.  
* Un servicio de infraestructura (ej. ITenantResolver) inyectado en el DbContext de Entity Framework Core lee estos claims del token de la petición en curso.  
* Se aplican Filtros de Consulta Globales (Global Query Filters) en EF Core de forma automática (e \=\> e.OperadorId \== tenantId), garantizando que cualquier consulta LINQ agregue de manera implícita el filtro WHERE por inquilino, mitigando el riesgo de fuga accidental de datos por olvido de los desarrolladores.

## 4\. Alternativas Consideradas y Motivo de su Descarte {#4.-alternativas-consideradas-y-motivo-de-su-descarte}

* **Multitenancy por Base de Datos (Database-per-tenant):** Se descartó debido a la complejidad operativa que representaría gestionar dinámicamente el aprovisionamiento de múltiples bases de datos relacionales en la nube, lo cual incrementaría significativamente el costo de infraestructura (evaluado contra el RNF de control de costos en la nube). Además, dificultaría el cálculo agregado de métricas globales si la plataforma deseara incorporarlas a futuro.  
* **Multitenancy por Esquema (Schema-per-tenant):** Aunque ofrece una buena separación lógica a nivel de base de datos, se descartó porque dificulta y complejiza la ejecución de migraciones con Entity Framework Core, especialmente cuando se busca mantener un proceso automatizado simple en los pipelines de despliegue continuo requeridos.  
* **Multitenancy Implícito (Oculto en infraestructura):** Se descartó ocultar el identificador del tenant exclusivamente a nivel de base de datos relacional. Se prefirió el modelo explícito en el dominio.  
* **Autenticación mediante un Identity Provider externo vía OpenID Connect:** se evaluó delegar la autenticación a un proveedor externo (ej. Auth0, Azure AD, Keycloak) en lugar de emitir los tokens con ASP.NET Identity. Se descartó porque no existe en el alcance del proyecto un requerimiento de SSO corporativo ni de login social, y porque introduce una dependencia externa adicional (disponibilidad, configuración y eventual costo del proveedor) que no se justifica para el tamaño de este sistema. ASP.NET Identity ya cubre de forma nativa la emisión de tokens JWT firmados con los claims necesarios (operadorId/comercioId). Lo que se resigna al descartarlo es autenticación multifactor y login social listos para usar, y revocación/monitoreo centralizado de sesiones entre múltiples aplicaciones (ninguno de los dos es exigido por la letra en su estado actual).

## 5\. Justificación y Consecuencias {#5.-justificación-y-consecuencias}

* **Validación desde el Dominio:** Llevar el operadorId (y el comercioOperadorId para el segundo nivel) en las entidades refleja que el aislamiento es una responsabilidad y regla de negocio del modelo de dominio, no solo un truco de base de datos.  
* **Pruebas Automatizadas (Testabilidad):** Este enfoque permite escribir pruebas unitarias de negocio puro que instancien objetos de dominio de diferentes inquilinos y validen matemáticamente que la lógica rechaza cruces de información, sin siquiera necesitar levantar la base de datos de test.  
* **Cumplimiento del Segundo Nivel:** Evita la duplicación de datos de comercios. Al separar "comercio" de "vínculo comercial" (ComercioOperador), un comercio puede estar suspendido por el Operador A pero seguir trabajando con el Operador B con absoluta normalidad.  
* **Consecuencias / Dificultades a futuro:** La principal consecuencia es que debemos garantizar que todas las nuevas entidades creadas implementen la interfaz de tenant (por ejemplo, IHasTenant) para que los Filtros de Consulta Globales se apliquen correctamente. Esto se mitigará introduciendo una Prueba de Arquitectura automatizada (como se requiere con NetArchTest / ArchUnitNET) que falle si una entidad transaccional se crea sin su correspondiente identificador de operador o comercio.

# Propuesta de Stack Tecnológico y Entorno de Desarrollo {#propuesta-de-stack-tecnológico-y-entorno-de-desarrollo}

Para dar cumplimiento a los requerimientos funcionales y no funcionales del sistema, se propone el siguiente conjunto de tecnologías y herramientas para el desarrollo, ejecución y observabilidad de la plataforma.

## 1 Entorno de Desarrollo Local {#1-entorno-de-desarrollo-local}

* **Entorno de Desarrollo Integrado (IDE):** Antigravity.  
* **Framework y SDK:** .NET 10 SDK, utilizado como base obligatoria para todo el ecosistema del proyecto.  
* **Infraestructura Local:** Docker y **docker compose** para la orquestación de servicios de soporte en el equipo de cada desarrollador (base de datos, caché, mensajería y herramientas de observabilidad), garantizando un entorno reproducible.

## 2 Plataforma y Capa de Presentación  {#2-plataforma-y-capa-de-presentación}

Se utilizará la arquitectura de un monolito modular para la API y las aplicaciones web.

* **Backoffice del operador:** ASP.NET Core Razor Pages.  
* **Portal del Comercio y Seguimiento Público:** Blazor (el equipo definirá mediante un ADR el modo de renderizado: Server, WebAssembly o Auto).  
* **Aplicación Móvil (Repartidores):** .NET MAUI implementando el patrón MVVM.  
* **Procesamiento en Segundo Plano:** un Worker Service en .NET (BackgroundService del Generic Host) desplegado de forma independiente de la API. Consume eventos de forma asíncrona desde la cola de mensajes, pero también accede directamente a PostgreSQL (recálculo de indicadores, cierre diario) y realiza llamadas HTTP salientes hacia servicios externos (webhooks de comercios, proveedores de email/SMS y push).

## 3 Persistencia de Datos {#3-persistencia-de-datos}

* **ORM Principal:** Entity Framework Core con manejo de migraciones controladas y control de concurrencia optimista.  
* **Base de Datos Transaccional:** PostgreSQL  
* **Base de Datos Móvil:** SQLite para el almacenamiento local cifrado en la aplicación del repartidor, permitiendo la operación completa sin conectividad.  
* **Caché Distribuido:** Redis, para soportar las altas frecuencias de lectura (ej. en el seguimiento público) y gestionar la expiración/invalidación de datos.

## 4 Mensajería Asíncrona y Tiempo Real {#4-mensajería-asíncrona-y-tiempo-real}

* **Cola de Mensajes (Message Broker):** RabbitMQ *(o Apache Kafka, si decidimos abordar el requerimiento opcional)* para garantizar la entrega de eventos bajo el patrón Outbox, manejando reintentos y colas de mensajes fallidos.  
* **Comunicación en Tiempo Real:** SignalR para la actualización en vivo del tablero de operación de los despachadores.

## 5 Seguridad {#5-seguridad}

* **Autenticación y Autorización:** ASP.NET Identity para la gestión de usuarios y la emisión de tokens JWT, con autorización basada en perfiles y políticas de aislamiento por inquilino (ver alternativa de OpenID Connect descartada en la sección de Multitenancy).

## 6 Observabilidad {#6-observabilidad}

Para instrumentar el sistema y lograr visibilidad operativa, se integrarán las siguientes herramientas:

* **Logging:** Serilog para el registro estructurado de eventos.  
* **Métricas y Trazas (Tracing):** OpenTelemetry para trazas distribuidas con un identificador de correlación de extremo a extremo.  
* **Visualización:** en desarrollo local, el dashboard de .NET Aspire; en producción (la VM de Oracle), el stack Grafana/Prometheus/Jaeger para centralizar la telemetría en un tablero técnico.

## 7 Despliegue e Infraestructura Cloud {#7-despliegue-e-infraestructura-cloud}

* **Infraestructura como Código (IaC):** Terraform para definir, provisionar y destruir los recursos en la nube de forma automatizada.  
* **CI/CD Pipeline:** GitHub Actions para integración y entrega continua (compilación, ejecución de pruebas unitarias/arquitectura y despliegue).  
* **Proveedor Cloud:** **Primera prioridad:** Oracle Cloud Infrastructure (OCI). Se desplegarán máquinas virtuales (Compute Instances) como servidores remotos reales en la nube pública para alojar las aplicaciones y servicios.  
   **Plan de contingencia:** dado el volumen de servicios que corren en simultáneo (API balanceada, base de datos, Redis, RabbitMQ/Worker), existe el riesgo de exceder los recursos de la capa gratuita de OCI. Si esto ocurre, el equipo migraría el despliegue a Google Cloud Platform (GCP) o AWS. En el caso de GCP, se usarían los créditos de prueba para desplegar con contenedores (Cloud Run o Compute Engine), manteniendo siempre un proveedor de nube pública real, sin depender de entornos locales ni túneles.

# ADR 01 \- Estilo Arquitectónico Interno y Organización del Código {#adr-01---estilo-arquitectónico-interno-y-organización-del-código}

## 1\. Contexto y Restricciones {#1.-contexto-y-restricciones-1}

La arquitectura de despliegue exigida es un monolito modular para la API y aplicaciones web, junto con un Worker en segundo plano. La restricción principal impuesta por la letra es que la lógica de dominio debe estar completamente aislada: no puede depender de la infraestructura, del acceso a datos ni de los frameworks de presentación. Además, esta regla debe ser verificable automáticamente en el pipeline de CI/CD mediante pruebas de arquitectura (ej. NetArchTest o ArchUnitNET).

## 2\. Decisión Adoptada {#2.-decisión-adoptada-1}

Se ha decidido adoptar Arquitectura Limpia (Clean Architecture) fuertemente orientada al Diseño Guiado por el Dominio (DDD), estructurando la solución en anillos concéntricos donde las dependencias fluyen estrictamente hacia adentro.

La solución se organizará en los siguientes proyectos o capas principales:

* Core / Domain: Entidades puras (ej. Envio, Bulto, Operador), objetos de valor y la máquina de estados declarativa. No tiene dependencias externas.  
* Application (Casos de Uso): Lógica de orquestación, puertos (interfaces) y manejo de comandos/consultas (CQRS ligero si aplica). Depende solo de Core.  
* Infrastructure: Implementación de los puertos (Entity Framework Core, acceso a RabbitMQ/Redis, llamadas HTTP salientes a los webhooks de comercios). Depende de Application.  
* Presentation / API: Controladores REST, SignalR Hubs y las Razor Pages del Backoffice. Depende de Application e Infrastructure (solo por inyección de dependencias).

## 3\. Alternativas Consideradas y Motivo de su Descarte {#3.-alternativas-consideradas-y-motivo-de-su-descarte}

* Arquitectura Tradicional en N-Capas (N-Tier): Se descartó de plano. En N-Capas, la capa de Negocio suele depender de la capa de Acceso a Datos (DAL). Esto violaría directamente la restricción de la letra que prohíbe que el dominio dependa de la infraestructura o acceso a datos.  
* Vertical Slice Architecture: Aunque es una alternativa moderna excelente para microservicios, se descartó porque la exigencia de modelar la solución como un "monolito modular" con reglas de dominio transversales muy estrictas (como el multitenancy explícito y la máquina de estados compartida por múltiples actores) se gestiona con mayor claridad y centralización mediante Arquitectura Limpia.

## 4\. Consecuencias {#4.-consecuencias}

* Facilidad de Pruebas: Permite escribir pruebas unitarias sobre el 100% de la lógica de dominio (transiciones de estado del envío, cálculo de tarifas) sin necesidad de levantar bases de datos ni mocks complejos de Entity Framework.  
* Cumplimiento de RNF: Permite la escritura de pruebas en NetArchTest de la forma: Types.InAssembly(Domain).ShouldNot().HaveDependencyOn(Infrastructure), cumpliendo el requerimiento no funcional 6.1.  
* Costo Inicial (Dificultad): Obliga al equipo a crear múltiples DTOs y mapeos entre la capa de presentación y la de dominio, lo que introduce un poco de "código repetitivo" (boilerplate) pero garantiza el desacoplamiento a largo plazo.

# 

# ADR 02 \- Modo de Renderizado para las Aplicaciones Blazor {#adr-02---modo-de-renderizado-para-las-aplicaciones-blazor}

## 1\. Contexto y Restricciones {#1.-contexto-y-restricciones-2}

La plataforma cuenta con dos aplicaciones web desarrolladas en Blazor: el Portal del Comercio y el Seguimiento Público. El Seguimiento Público es el componente de mayor volumen de tráfico del sistema. Dado que la arquitectura exigida es un monolito modular que agrupa la API y las aplicaciones web, el requerimiento de ejecutar al menos dos instancias simultáneas detrás de un balanceador de carga aplica a todo el conjunto de forma indivisible. En este contexto, se debe decidir el modo de renderizado (Server, WebAssembly o Auto) justificando su impacto en el consumo de recursos del servidor, el manejo del estado, la latencia y el escalado horizontal. 

## 2\. Decisión Adoptada {#2.-decisión-adoptada-2}

Se ha decidido utilizar el modo de renderizado **WebAssembly (WASM)** para ambas aplicaciones Blazor.

## 3\. Análisis y Justificación {#3.-análisis-y-justificación}

La elección se fundamenta en el análisis de los cuatro pilares requeridos:

* **Consumo de recursos del servidor:** Blazor Server mantiene un circuito activo (conexión SignalR) y el estado de la interfaz en la memoria del servidor por cada usuario conectado. Dado que el Seguimiento Público concentra un alto volumen de tráfico, Blazor Server saturaría rápidamente la memoria. WebAssembly, por el contrario, descarga la aplicación directamente al navegador del destinatario o comercio, delegando el procesamiento al cliente y liberando casi por completo los recursos del servidor.  
* **Escalado horizontal:** Para cumplir con el balanceo de carga exigido, administrar el tráfico de red en las subredes y Virtual Cloud Networks hacia las instancias de cómputo Ubuntu en Oracle Cloud resulta significativamente más sencillo con WebAssembly. Al consumir una API stateless (sin estado), cualquier instancia puede responder cualquier petición. Si se eligiera Blazor Server, el balanceador de carga obligaría a configurar sticky sessions (afinidad de sesión) y un backplane de Redis para mantener vivos los WebSockets entre las distintas máquinas virtuales, sumando una complejidad de infraestructura innecesaria.  
* **Manejo de estado:** En WebAssembly, el estado de la UI vive de forma natural en el navegador del cliente, reduciendo drásticamente el impacto de la desconexión.  
* **Latencia:** Si bien WebAssembly tiene una penalización de latencia en la primera carga (porque debe descargar el runtime de .NET al navegador), la latencia durante la navegación posterior es casi nula. En Blazor Server, cada clic o interacción de la interfaz debe viajar por la red hacia el servidor para procesarse y volver, volviendo la experiencia muy susceptible a conexiones inestables.

## 4\. Alternativas Consideradas y Motivo de su Descarte {#4.-alternativas-consideradas-y-motivo-de-su-descarte-1}

* **Blazor Server:** Se descartó porque monopoliza la memoria del servidor bajo picos de tráfico y vuelve muy compleja la orquestación del balanceo de carga entre las múltiples instancias exigidas.  
* **Blazor Auto (Global):** Aunque ofrece lo mejor de ambos mundos (carga inicial rapidísima con Server y luego transición a WebAssembly en segundo plano), se descartó porque obliga de todos modos a soportar y configurar toda la infraestructura subyacente requerida por Blazor Server (SignalR, protección de datos compartida y *sticky sessions*) en los balanceadores de red, lo cual sobrecarga las tareas de operaciones tecnológicas del equipo.

# Plan de Trabajo y Registros de Decisión de Arquitectura {#plan-de-trabajo-y-registros-de-decisión-de-arquitectura}

## 1\. Plan de trabajo {#1.-plan-de-trabajo}

### 1.1 Integrantes y áreas de responsabilidad {#1.1-integrantes-y-áreas-de-responsabilidad}

El equipo está compuesto por cuatro integrantes. La división por área no implica compartimentos estancos: los cuatro participan del modelado de dominio y de las decisiones de arquitectura, pero cada uno lidera y rinde cuentas de su componente en los monitoreos y en la defensa individual.

| Integrante | Área principal | Aplicaciones / componentes a cargo |
| ----- | ----- | ----- |
| **Anthony** | Backend | Módulo Operación: ciclo de vida del envío, planificación y despacho de rutas |
| **Francisco** | Backend | Módulo Configuración/Administración y plataforma transversal: multitenancy, caché, mensajería/Worker, seguridad, IaC/despliegue, observabilidad |
| **Felipe** | Frontend | Backoffice del operador (Razor Pages), Portal del comercio y Seguimiento público (Blazor) \+ 2-3 casos de uso de backend |
| **Joaquín** | App móvil | Aplicación del repartidor (.NET MAUI), operación offline-first y sincronización \+ 2-3 casos de uso de backend |

### 1.2 Responsabilidades detalladas por integrante {#1.2-responsabilidades-detalladas-por-integrante}

Anthony y Francisco dividen el backend por módulo funcional completo (dominio, aplicación y persistencia de su módulo), en lugar de dividir por capa, de modo que cada uno pueda avanzar de punta a punta sin bloquearse esperando al otro. Felipe y Joaquín, además de su frontend/app, toman 2 o 3 casos de uso de backend para familiarizarse (en ambos casos, casos de uso ligados a lo que su propio cliente va a consumir).

### 1.3 Cronograma de trabajo por hito {#1.3-cronograma-de-trabajo-por-hito}

El cronograma sigue las instancias de monitoreo obligatorias definidas en la letra (sección 8.3). Cada hito indica el objetivo verificable y el foco de trabajo de cada integrante en esa etapa. Las tareas de modelado de dominio, documentación y registro de horas son transversales a los cuatro integrantes durante todo el laboratorio.

### 1.4 Registro de horas {#1.4-registro-de-horas}

Cada integrante registrará semanalmente sus horas discriminadas por tipo de actividad (análisis, desarrollo, infraestructura, pruebas, documentación e investigación), conforme exige la letra. El registro se centraliza en una planilla compartida y se adjunta a la entrega final.