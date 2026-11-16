# Plataforma Multioperador de Distribución de Última Milla

Taller de Sistemas de Información .NET — UTEC, Tecnólogo en Informática (edición 2026).
Equipo: Joaquín Bassini · Felipe Bernardi · Anthony Curbelo · Francisco Lema.

Un único despliegue atiende a varios operadores logísticos con aislamiento estricto por inquilino (ADR-002). Cada operador administra su identidad visual, zonas y franjas horarias, cuadro tarifario versionado, reglas operativas, motivos de no entrega y usuarios. Los comercios cargan envíos y retiros por portal, archivo CSV o API de integración; el operador planifica, despacha y controla en tiempo real; el repartidor opera en campo con o sin conexión; y el destinatario consulta y reprograma su paquete desde un enlace público.

## Aplicaciones

| Aplicación | Tecnología | Proyecto | Puerto local |
|---|---|---|---|
| API (monolito modular) | ASP.NET Core 10, Minimal APIs, SignalR | `src/Api` | `5000` |
| Backoffice del operador | ASP.NET Core Razor Pages | `src/Web/Backoffice` | `5002` |
| Portal del comercio | Blazor WebAssembly | `src/Web/PortalComercio*` | `5003` |
| Seguimiento público | Blazor WebAssembly, sin autenticación | `src/Web/SeguimientoPublico` | `5004` |
| Aplicación del repartidor | .NET MAUI (MVVM, SQLite + SQLCipher) | `src/Mobile/UltimaMilla.Mobile` | — |
| Worker en segundo plano | .NET Worker Service (Outbox + RabbitMQ + Webhooks) | `src/Worker` | — |

Arquitectura limpia por capas (`Domain` <- `Application` <- `Infrastructure` / `Api` / `Worker`), con reglas de dependencias verificadas automáticamente en `tests/UltimaMilla.Architecture.Tests`. Ver registros de decisión en [`docs/adr/README.md`](docs/adr/README.md).

## Comportamientos a tener en cuenta

- **Tiempo real:** toda escritura en la API se anuncia por SignalR y el Backoffice, el Portal y el seguimiento público se actualizan solos, incluidos los paneles abiertos (por ejemplo la línea de vida de un envío). Solo las pantallas de alta y edición quedan fuera, para no pisar un formulario en uso.
- **Sesión única:** una cuenta solo tiene una sesión activa; iniciar sesión en otro lugar deja sin efecto la anterior (`401 SESION_DESPLAZADA`) y cada aplicación la lleva al inicio de sesión con un aviso. Con cuentas compartidas, la segunda persona expulsa a la primera.
- **Cédula:** exactamente 8 dígitos, solo números (destinatarios, repartidores y usuarios).
- **Modalidad Express:** se tarifica con la regla "Urgente" del cuadro tarifario del operador.
- **Cobertura:** los envíos y los retiros a domicilio solo se aceptan dentro de las zonas activas del operador.

## Requisitos

- **.NET SDK 10** (versión fijada en `global.json`).
- **Docker Desktop** (o Docker Engine + Compose v2) para ejecución local en contenedores y pruebas de integración con PostgreSQL.
- **Cargas de trabajo de .NET MAUI** (`dotnet workload install maui`) y Android SDK únicamente si se compila/ejecuta la app móvil.
- **Terraform >= 1.5** y **Azure CLI (`az`)** con sesión iniciada para aprovisionar o reconstruir la infraestructura remota en Azure.

## Ejecución en local

### Opción 1: Entorno completo con Docker Compose (recomendado)

Desde la raíz del repositorio:

```bash
docker compose up -d --build
```

Levanta PostgreSQL 16, Redis 7, RabbitMQ 3 (con gestión), API, Worker, Backoffice, Portal del Comercio y Seguimiento Público:

| Servicio | URL local |
|---|---|
| API y documentación Swagger | `http://localhost:5000/swagger` |
| Salud de la API (`/live`, `/ready`) | `http://localhost:5000/ready` |
| Backoffice del operador | `http://localhost:5002` |
| Portal del comercio | `http://localhost:5003` |
| Seguimiento público | `http://localhost:5004` |
| RabbitMQ Management (`guest` / `guest`) | `http://localhost:15672` |

Para detener el entorno local:

```bash
docker compose down
```

### Opción 2: Desarrollo frontend con Hot Reload (`dotnet watch`)

Si solo se quiere trabajar sobre las aplicaciones web (en modo Mocks en memoria o conectadas a la API local en `http://localhost:5000`):

```powershell
# Menú interactivo (Windows):
.\scripts\ejecutar-frontend.bat

# Iniciar los 3 frontends contra la API real (requiere Postgres/Redis/API activos):
.\scripts\ejecutar-frontend.ps1 -Aplicacion todas -UsarMocks:$false
```

### Opción 3: Aplicación móvil (.NET MAUI)

Por defecto (`Configuration/ApiEndpoints.cs`), la app apunta al entorno desplegado en la nube (`https://shippingbox.duckdns.org`). Para apuntar a la API local, configurar la URL desde la pantalla de configuración de servidor antes de iniciar sesión (`http://localhost:5000` en Windows o `http://10.0.2.2:5000` en emulador Android):

```bash
# Ejecutar en Windows:
dotnet build src/Mobile/UltimaMilla.Mobile/UltimaMilla.Mobile.csproj -t:Run -f net10.0-windows10.0.19041.0

# Compilar para Android (emulador o dispositivo conectado por ADB):
dotnet build src/Mobile/UltimaMilla.Mobile/UltimaMilla.Mobile.csproj -t:Run -f net10.0-android
```

## Cuentas de demostración

En `localhost` con `ASPNETCORE_ENVIRONMENT=Development`, la solución permite el acceso directo de desarrollo; además, al iniciar la base de datos por primera vez se siembran automáticamente las siguientes cuentas de prueba (en producción solo se habilitan si `SEED_PASSWORD_DEMO` está definida con mínimo 12 caracteres y toman esa contraseña):

| Operador / Alcance | Perfil | Correo | Contraseña (dev) |
|---|---|---|---|
| Operador 1 ("Última Milla Express") | Administrador | `admin@gmail.com` | `Admin.2026!` |
| Operador 1 ("Última Milla Express") | Despachador | `despacho@gmail.com` | `Despacho.2026!` |
| Operador 1 ("Última Milla Express") | Operario de depósito | `deposito@gmail.com` | `Deposito.2026!` |
| Operador 1 ("Última Milla Express") | Repartidor | `repartidor@gmail.com` | `Repartidor.2026!` |
| Operador 1 ("MegaStore Uruguay S.R.L.") | Comercio | `comercio@gmail.com` | `Comercio.2026!` |
| Operador 2 ("TransLogística Express Norte") | Administrador | `admin.express@ultimamilla.com.uy` | `demo1234` |
| Operador 2 ("TransLogística Express Norte") | Despachador | `despacho.express@ultimamilla.com.uy` | `demo1234` |
| Operador 2 alternativo (`Operador2Id`) | Administrador | `admin2@gmail.com` | `Admin.2026!` |
| Plataforma | Administrador de plataforma | `plataforma@gmail.com` | `Plataforma.2026!` |

Envíos semilla para pruebas rápidas: `ENV-2026-00108`, `ENV-2026-00109`, `ENV-2026-00110` (Operador 1) y `ENV-2026-00201`, `ENV-2026-00202` (Operador 2).

## Pruebas automatizadas

Para ejecutar los **6 proyectos de pruebas** de la solución completa (Domain, Application, Architecture, Backoffice, Frontend e Integration):

```bash
dotnet test UltimaMilla.sln
```

Ejecuciones acotadas:

```bash
dotnet test UltimaMilla.Backend.slnf -c Release   # Dominio, aplicación, arquitectura e integración
dotnet test tests/UltimaMilla.Backoffice.Tests   # Páginas y seguridad del Backoffice
dotnet test tests/UltimaMilla.Frontend.Tests     # Contratos, clientes HTTP y componentes Blazor
```

Las pruebas de `UltimaMilla.Integration.Tests` requieren PostgreSQL disponible en `localhost:5432` (`docker compose up -d postgres`); cuando no hay instancia accesible, los casos `[FactPostgres]` se omiten automáticamente. Más detalle sobre cobertura, carga y resiliencia en [`docs/Plan_de_Pruebas.md`](docs/Plan_de_Pruebas.md).

## Despliegue remoto en la nube (Azure)

La plataforma productiva se ejecuta sobre `docker-compose.prod.yml` en una VM de Azure con **2 instancias de la API (`api1`, `api2`)**, balanceador y TLS automático en **Caddy**, **Redis** (sesiones activas, caché distribuido y backplane SignalR), **PostgreSQL 16**, **RabbitMQ**, **Worker**, **Prometheus** y **Grafana**:

- **Backoffice y API:** `https://shippingbox.duckdns.org` (`https://shippingbox.duckdns.org/ready`)
- **Portal del comercio:** `https://shippingbox-comercio.duckdns.org`
- **Seguimiento público:** `https://seguimiento.shippingbox.duckdns.org`
- **Tablero de observabilidad (Grafana):** `https://panel.shippingbox.duckdns.org`
- **Descarga OTA del APK móvil:** `https://shippingbox.duckdns.org/descargas/`

### 1. Entrega continua (GitHub Actions)

Al fusionar en `main`, [`.github/workflows/ci.yml`](.github/workflows/ci.yml) compila en `Release`, audita vulnerabilidades (`dotnet list package --vulnerable`), ejecuta todas las pruebas verificando cobertura > 70 % en `Domain + Application`, publica las imágenes en `ghcr.io/jakz13/ultimamilla/*` y despliega en la VM mediante `scripts/desplegar.sh` con actualización por turnos (`reiniciar-remoto.sh`).

### 2. Despliegue manual desde esta PC

Requisitos: Docker Desktop activo y clave SSH autorizada en la VM (`~/.ssh/azure_ultimamilla`). Si la PC usa una clave nueva, quien administra la VM debe autorizar su `.pub` con `scripts\autorizar-clave.ps1`.

En **Windows**:

```powershell
# Desplegar todos los servicios (api, worker, backoffice, portal, seguimiento y panel):
.\scripts\desplegar.bat

# Desplegar servicios puntuales:
.\scripts\desplegar.bat api worker
.\scripts\desplegar.ps1 -Servicios api,backoffice -DesdeRegistro
```

En **Linux / macOS**:

```bash
export VM_HOST=<IP_DE_LA_VM>
bash scripts/desplegar.sh                       # Todos los servicios
bash scripts/desplegar.sh api backoffice portal # Servicios puntuales
```

### 3. Actualización de la app del repartidor (OTA)

Caddy sirve el APK firmado y `version.json` desde `~/shippingbox/descargas` en la VM (montado solo lectura). Para publicar una nueva versión tras compilar y firmar el APK en `artifacts\UltimaMilla-Release-Signed.apk` con un `ApplicationVersion` incremental en `UltimaMilla.Mobile.csproj`:

```powershell
.\scripts\desplegar.bat mobile
# Equivalente directo:
.\scripts\publicar-apk.ps1 -Notas "Descripción de la actualización"
```

El script valida firma y paquete, calcula SHA-256 y tamaño, sube primero el APK y al final `version.json` con `Cache-Control: no-store`, y verifica ambos endpoints por HTTPS. Tras reconstruir la VM con `scripts\reconstruir.ps1` debe volver a publicarse el APK. Detalle en [`docs/adr/ADR-007-despliegue-iac-y-balanceo.md`](docs/adr/ADR-007-despliegue-iac-y-balanceo.md) y [`docs/Pendientes_Mobile.md`](docs/Pendientes_Mobile.md).

### 4. Correo SMTP y persistencia de claves de sesión

- **Correo a destinatarios (SMTP):** las credenciales viven únicamente en `~/shippingbox/.env.prod` de la VM (`SMTP_HOST`, `SMTP_PORT`, `SMTP_SSL`, `SMTP_USER`, `SMTP_PASS`, `SMTP_FROM`), nunca en el repositorio. Si no están definidas, el correo no se envía y no bloquea la operación. Al reconstruir con `scripts\reconstruir.ps1`, conviene exportarlas como variables de entorno en la PC para copiarlas automáticamente a la VM nueva. El enlace del correo apunta al dominio de seguimiento público (`DOMINIO_SEGUIMIENTO`).
- **Claves de sesión:** el Backoffice y el Portal del Comercio guardan sus claves de Data Protection en volúmenes Docker (`backoffice_keys`, `portal_keys`) para que un redespliegue no invalide formularios ni sesiones abiertas.

### 5. Infraestructura como Código (destrucción y reconstrucción total)

Definida con Terraform en `infra/terraform-azure` (`rg-ultimamilla-prod`, VNet, subred, NSG con puertos 22/80/443 únicamente, IP pública estática, VM Linux con `cloud-init.yaml.tftpl` que instala Docker y genera secretos aleatorios en `~/shippingbox/.env.prod`).

```powershell
# 1. Requisitos previos en la sesión:
az login
$env:DUCKDNS_TOKEN = "<TOKEN_DUCKDNS>"
$env:SEED_PASSWORD_DEMO = "<PASSWORD_MINIMO_12_CARACTERES>"

# 2. Destruir el ambiente completo:
cd infra\terraform-azure
terraform destroy -auto-approve -var-file="ambientes/prod.tfvars"
cd ..\..

# 3. Reconstruir desde cero con un comando (Terraform + DNS DuckDNS + cloud-init + Docker Compose + /ready):
.\scripts\reconstruir.ps1

# 4. Republicar el APK OTA en la VM recién creada:
.\scripts\desplegar.bat mobile
```

Detalle completo y control de costos en [`infra/terraform-azure/README.md`](infra/terraform-azure/README.md).

## Scripts disponibles (`scripts/`)

| Script | Propósito |
|---|---|
| `scripts/ejecutar-frontend.ps1` / `.bat` | Ejecuta Backoffice (`5002`), Portal (`5003`) y Seguimiento (`5004`) con Hot Reload (mocks o API real). |
| `scripts/desplegar.ps1` / `.bat` | Construye, envía o descarga imágenes y reinicia la VM de Azure sin caída (`api1` -> `api2`). |
| `scripts/desplegar.sh` | Equivalente POSIX para Linux/macOS y pipeline de GitHub Actions. |
| `scripts/reiniciar-remoto.sh` | Script ejecutado en la VM para hacer rolling restart por servicio respetando healthchecks. |
| `scripts/publicar-apk.ps1` | Publica y verifica el APK firmado + `version.json` para actualización OTA. |
| `scripts/reconstruir.ps1` | Reconstrucción integral: Terraform apply -> DuckDNS -> espera `cloud-init` -> despliegue -> `/ready`. |
| `scripts/actualizar-dns.ps1` | Actualiza registros A en DuckDNS y espera propagación antes de levantar Caddy/Let's Encrypt. |
| `scripts/autorizar-clave.ps1` | Autoriza una clave SSH pública nueva en la VM de producción. |
| `scripts/demo-correlacion.ps1` | Demuestra trazabilidad de extremo a extremo con `X-Correlation-Id`. |
| `scripts/simular-repartidor.ps1` | Simula telemetría y flujo móvil contra la API. |

## Documentación del proyecto

| Documento | Contenido |
|---|---|
| [`docs/Letra_del_Laboratorio.md`](docs/Letra_del_Laboratorio.md) | Letra y alcance funcional/no funcional del laboratorio |
| [`Documento de Análisis y Diseño.md`](Documento%20de%20An%C3%A1lisis%20y%20Dise%C3%B1o.md) | Análisis inicial, estrategia multitenancy y stack tecnológico |
| [`docs/Documento_Tecnico_RNF.md`](docs/Documento_Tecnico_RNF.md) | Implementación detallada de los requerimientos no funcionales |
| [`docs/adr/README.md`](docs/adr/README.md) | Registros de decisión de arquitectura (ADR-001 a ADR-008) |
| [`docs/Plan_de_Pruebas.md`](docs/Plan_de_Pruebas.md) | Estrategia de pruebas unitarias, integración, carga y cobertura |
| [`docs/Pruebas_de_Resiliencia.md`](docs/Pruebas_de_Resiliencia.md) | Pruebas ante caída de dependencias (API, Redis, RabbitMQ, Postgres, webhooks) |
| [`docs/Seguridad.md`](docs/Seguridad.md) | Medidas de seguridad, JWT, anti-IDOR, cifrado SQLCipher y OWASP |
| [`docs/Estado_de_Requerimientos.md`](docs/Estado_de_Requerimientos.md) | Matriz de cumplimiento de requerimientos |
| [`docs/API_Publica.md`](docs/API_Publica.md) | Documentación de la API de integración para comercios |
| [`docs/UC-44_Observabilidad_Tablero_Tecnico.md`](docs/UC-44_Observabilidad_Tablero_Tecnico.md) | Diseño del tablero de observabilidad (Prometheus, Grafana y OpenTelemetry) |
| [`infra/terraform-azure/README.md`](infra/terraform-azure/README.md) | Guía operativa de Terraform en Azure y control de costos |
