# Plataforma Multioperador de Distribución de Última Milla

Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática (edición 2026).
Equipo: Joaquín Bassini · Felipe Bernardi · Anthony Curbelo · Francisco Lema.

Un único despliegue atiende a varios operadores logísticos. Cada uno tiene su marca, sus zonas, su cuadro tarifario, sus reglas y sus usuarios, sin acceso a los datos de los demás. Los comercios cargan envíos y retiros y los siguen; el operador planifica y controla la jornada; el repartidor trabaja en la calle con o sin conexión; el destinatario sigue su paquete con un enlace público.

## Aplicaciones

| Aplicación | Tecnología | Proyecto |
|---|---|---|
| API (monolito modular) | ASP.NET Core 10, minimal APIs, SignalR | `src/Api` |
| Backoffice del operador | ASP.NET Core Razor Pages | `src/Web/Backoffice` |
| Portal del comercio | Blazor WebAssembly | `src/Web/PortalComercio*` |
| Seguimiento público | Blazor, sin autenticación | `src/Web/SeguimientoPublico` |
| Aplicación del repartidor | .NET MAUI (MVVM, SQLite cifrado) | `src/Mobile` |
| Worker en segundo plano | .NET Worker Service, desplegado aparte | `src/Worker` |

Arquitectura por capas (`Domain` ← `Application` ← `Infrastructure`/`Api`/`Worker`), con la regla de dependencias verificada por pruebas automáticas. Ver los registros de decisión en [`docs/adr`](docs/adr/README.md).

## Requisitos
.NET SDK 10 (`global.json`), Docker Desktop y, para la app móvil, las cargas de trabajo de MAUI.

## Ejecutar en local
```bash
docker compose up -d --build        # Postgres, Redis, RabbitMQ, API, worker, backoffice y portal
```
| Servicio | URL |
|---|---|
| API y documentación Swagger | http://localhost:5000/swagger |
| Backoffice | http://localhost:5002 |
| Portal del comercio | http://localhost:5003 |
| RabbitMQ (gestión) | http://localhost:15672 |

En `localhost` el **modo demo** evita el inicio de sesión. Para probar con sesión real, iniciar sesión en cada aplicación con las cuentas de demostración:

| Perfil | Correo | Contraseña |
|---|---|---|
| Administrador (operador 1) | admin@gmail.com | Admin.2026! |
| Despachador | despacho@gmail.com | Despacho.2026! |
| Operario de depósito | deposito@gmail.com | Deposito.2026! |
| Repartidor | repartidor@gmail.com | Repartidor.2026! |
| Comercio | comercio@gmail.com | Comercio.2026! |
| Administrador (operador 2) | admin2@gmail.com | Admin.2026! |

Son datos de demostración que siembra la aplicación al arrancar; **deben cambiarse en cualquier entorno real**.

## Pruebas
```bash
dotnet test UltimaMilla.Backend.slnf -c Release     # dominio, aplicación, arquitectura e integración
dotnet test tests/UltimaMilla.Frontend.Tests        # componentes y clientes del frontend
```
Las pruebas de integración necesitan Postgres local (`docker compose up -d postgres`); si no hay uno disponible se omiten. El detalle de qué se prueba, cómo medir la cobertura y las pruebas de carga y de resiliencia está en [`docs/Plan_de_Pruebas.md`](docs/Plan_de_Pruebas.md).

## Despliegue en la nube (Azure)
- **Producción:** https://shippingbox.duckdns.org (backoffice y API) · https://shippingbox-comercio.duckdns.org (portal) · https://panel.shippingbox.duckdns.org (Grafana).
- **Infraestructura como código:** `infra/terraform-azure` (grupo de recursos, red, reglas de seguridad, IP pública, VM y preparación del servidor). Plan, aplicación, destrucción y reconstrucción con un comando: ver [`infra/terraform-azure/README.md`](infra/terraform-azure/README.md). `infra/terraform` es el código histórico de Oracle Cloud.
- **Aplicación:** Docker Compose de producción (`docker-compose.prod.yml`) con dos instancias de la API detrás de Caddy (HTTPS automático y chequeos de salud).
- **Entrega continua:** al fusionar en `main`, GitHub Actions compila, prueba, mide la cobertura y despliega. Despliegue manual: `scripts\desplegar.bat [api|worker|backoffice|portal|panel]`.
- **Demostración de la correlación de extremo a extremo:** `scripts\demo-correlacion.ps1`.

## Documentación
| Documento | Contenido |
|---|---|
| [`Documento de Análisis y Diseño.md`](Documento%20de%20An%C3%A1lisis%20y%20Dise%C3%B1o.md) | Análisis inicial, multitenancy y stack |
| [`Documento_Requerimientos.md`](Documento_Requerimientos.md) | Requerimientos funcionales |
| [`docs/adr`](docs/adr/README.md) | Registros de decisión de arquitectura |
| [`docs/Plan_de_Pruebas.md`](docs/Plan_de_Pruebas.md) | Estrategia, ejecución y cobertura de pruebas |
| [`docs/Estado_de_Requerimientos.md`](docs/Estado_de_Requerimientos.md) | Qué está implementado y qué falta, requerimiento por requerimiento |
| [`docs/UC-44_Observabilidad_Tablero_Tecnico.md`](docs/UC-44_Observabilidad_Tablero_Tecnico.md) | Caso de uso del tablero técnico |
| [`CLAUDE.md`](CLAUDE.md) | Reglas del proyecto para asistentes de IA |
