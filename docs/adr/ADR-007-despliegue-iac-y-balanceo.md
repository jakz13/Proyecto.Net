# ADR-007: Despliegue en la nube, infraestructura como código y balanceo de carga

**Estado:** aprobado · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.13 y 6.14

## 1. Contexto y restricciones
Se exige un servidor remoto real en una nube pública, HTTPS válido, al menos dos instancias de la API balanceadas con continuidad ante la caída de una, infraestructura definida por código con variables por ambiente, pipeline que compile, pruebe y despliegue, y control de costo. Se trabaja dentro del nivel gratuito de Oracle Cloud: una VM `VM.Standard.E2.1.Micro` (1 GB de RAM, CPU compartida).

## 2. Decisión
- **Una VM** con Docker Compose (`docker-compose.prod.yml`): dos instancias de la API (`api1`, `api2`), worker independiente, backoffice, portal del comercio, Postgres, Redis, RabbitMQ, Prometheus, Grafana y Caddy. Memoria acotada por contenedor y 4 GB de *swap*.
- **Caddy** como proxy inverso y balanceador: HTTPS automático (Let's Encrypt) para tres dominios, reparto con afinidad por cookie y **chequeos activos de salud** sobre `/ready`. Ante la caída de una instancia saca del reparto a esa y sigue con la otra; ante fallos aislados de un chequeo en una VM con CPU escasa tolera 3 consecutivos antes de retirarla.
- **API sin estado en memoria:** sesiones por JWT, caché y *backplane* de SignalR en Redis, estado en Postgres. Puntos `/health`, `/ready`, `/live`, apagado controlado y tareas programadas idempotentes (índice único del cierre diario por operador y día).
- **Reinicio escalonado (*rolling restart*):** se reinicia `api1`, se espera que esté sana y recién entonces `api2`; el servicio no se corta. Las imágenes se construyen fuera de la VM (1 GB no alcanza para compilar) y se transfieren.
- **Terraform para Azure** (`infra/terraform-azure`, proveedor `azurerm`): es el ambiente **actual** (el despliegue migró de Oracle a Azure el 11/11/2026). Define grupo de recursos, red, reglas (solo 22/80/443), IP estática y la VM `Standard_B2als_v2` con `cloud-init`; `validate` y `plan` verificados, costo estimado en su README. El código anterior para Oracle quedó en `infra/terraform` como histórico.
- **Terraform para Oracle (histórico)** (`infra/terraform`, proveedor OCI): red, reglas de seguridad y la VM con `cloud-init` que instala Docker y deja el `.env` con secretos generados en el servidor. Variables por ambiente en `ambientes/*.tfvars`; plan, aplicación y destrucción documentados.
- **CI/CD (GitHub Actions):** compila, ejecuta todas las pruebas (dominio, aplicación, arquitectura, integración y aislamiento), mide la cobertura, audita paquetes vulnerables y, solo en `main`, despliega por SSH con clave de un entorno protegido.
- **Distribución de la app del repartidor (OTA):** Caddy sirve `/descargas/*` desde una carpeta del host (`~/shippingbox/descargas`, montada en `/srv/descargas` en solo lectura) en el dominio principal, antes del proxy hacia la API: el APK firmado y `version.json` (versionCode, versionName, apkUrl, sha256, tamaño, notas). `version.json` se entrega con `Cache-Control: no-store` y el `.apk` con su tipo MIME. Se publica con `scripts/publicar-apk.ps1` (`desplegar.ps1 mobile`), nunca de forma automática en un despliegue normal. No toca la API ni la base de datos.
- **Costo (Azure):** ≈ 1,40 USD por día (VM `Standard_B2als_v2`, disco de 64 GB e IP estática; detalle y fuentes en `docs/auditoria/INFORME.md`). La evidencia se toma de *Cost Management* del portal de Azure.

## 3. Alternativas consideradas
- **Servir el APK desde el Backoffice, GitHub Releases o Azure Blob Storage:** el primero acopla la app móvil al despliegue web (cada versión exigiría una imagen nueva); el segundo necesita repositorio público o un token dentro de la app; el tercero agrega recursos, costo y otro punto de falla a Terraform. Servir un archivo estático desde Caddy no suma componentes.
- **Balanceador administrado de Oracle (histórico):** disponible en el nivel gratuito pero con ancho de banda muy limitado; se priorizó Caddy, que además resuelve el certificado.
- **Kubernetes / escalado automático:** descartados por recursos (1 GB de RAM); el autoescalado es un opcional que no se abordó.
- **Túneles (ngrok, Cloudflare Tunnel) o servir desde un equipo propio:** expresamente prohibidos por la letra.

## 4. Consecuencias
- Hay un **único punto de falla de hardware** (una VM): la alta disponibilidad es entre procesos, no entre máquinas. La destrucción y reconstrucción por Terraform es el plan ante la pérdida de la VM.
- La CPU compartida produce picos de latencia bajo carga; se documentan en las pruebas de carga.
- El ambiente de producción lo crea y gestiona el Terraform de Azure; la reconstrucción completa con un comando se midió en ≈ 8 minutos (`docs/auditoria/INFORME.md`).
- La carpeta `descargas` vive solo en la VM: **una reconstrucción (escenario 7) la deja vacía** y hay que volver a ejecutar `scripts/publicar-apk.ps1`. Montarla por primera vez recrea el contenedor de Caddy (unos segundos sin servicio en todos los dominios); los certificados no se pierden porque viven en el volumen `caddy_data`.
- Las contraseñas del keystore de firma están hoy escritas en el `.csproj` de la app (regla 6 de `CLAUDE.md`: sin secretos en el repositorio): deben pasarse por variable de entorno y cambiarse.
