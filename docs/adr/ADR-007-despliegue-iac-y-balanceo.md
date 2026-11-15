# ADR-007: Dónde desplegamos, cómo definimos la infraestructura y cómo balanceamos la API

**Estado:** Aprobado · **Fecha:** 2026-11-15 · **Responsable:** Anthony Curbelo · **Requerimientos:** 6.13 y 6.14

## 1. Contexto y problema
Necesitábamos un servidor real en la nube con HTTPS válido, al menos dos instancias de la API balanceadas (si una cae, la otra sigue respondiendo), infraestructura definida por código, un pipeline que compile, pruebe y despliegue, distribución directa de la app móvil (APK) y control estricto de costos.

Inicialmente arrancamos en la capa gratuita de Oracle Cloud (1 GB de RAM) y el 11/11/2026 migramos el ambiente a Microsoft Azure (con crédito de *Azure for Students*).

## 2. Decisión
- **Contenedores en una VM con `docker-compose.prod.yml`:** corren dos instancias de la API (`api1` y `api2`), `worker`, `backoffice`, `portal-comercio`, `seguimiento-publico`, `postgres`, `redis`, `rabbitmq`, `prometheus`, `grafana` y `caddy`, todos con tope de memoria (`mem_limit`).
- **Caddy como balanceador y HTTPS automático:** obtiene certificados Let's Encrypt para los 4 dominios, reparte tráfico entre `api1` y `api2` y revisa `/ready` cada 10 s. Si una instancia falla 3 veces seguidas, la retira automáticamente y deja funcionando la otra.
- **API sin estado y reinicio escalonado:** la API no guarda sesiones ni caché en memoria local (usa JWT, PostgreSQL y Redis). Al desplegar (`scripts/desplegar.bat` / `scripts/desplegar.sh`), primero se reinicia `api1`, se espera a que esté sana y recién después `api2` para no cortar el servicio.
- **Infraestructura como código (Terraform):**
  - En `infra/terraform-azure/` está la definición **actual** para Azure (grupo de recursos, red, firewall solo en puertos 22/80/443, IP pública estática, VM `Standard_B2als_v2` y `cloud-init` que instala Docker y genera secretos en `.env.prod`). Está aplicada y probada con reconstrucción completa en ≈ 8 minutos (`scripts/reconstruir.ps1`).
  - En `infra/terraform/` quedó el Terraform de Oracle Cloud como histórico.
- **CI/CD (GitHub Actions):** compila, audita vulnerabilidades, corre las pruebas, exige ≥ 70 % de cobertura en Dominio y Aplicación, publica imágenes en `ghcr.io` y despliega por SSH al hacer push a `main`.
- **Distribución de la app móvil (OTA):** Caddy sirve `/descargas/*` desde `~/shippingbox/descargas` en solo lectura con el APK firmado y `version.json` (`sha256`, tamaño y versión, con `Cache-Control: no-store`). Se publica con `scripts/publicar-apk.ps1` (`desplegar.ps1 mobile`) sin tocar la API ni la base.
- **Costo en Azure:** ≈ **1,40 USD/día** (VM `Standard_B2als_v2`, disco de 64 GB e IP estática; evidencia en *Cost Management* de Azure y `docs/auditoria/INFORME.md`).

## 3. Alternativas consideradas
- **Servir el APK desde Backoffice, GitHub Releases o Azure Blob Storage:** descartadas porque acoplan el despliegue web a la app móvil, exigen repo público/tokens embebidos o suman recursos y costos en Terraform; servir archivos estáticos desde Caddy es más simple y barato.
- **Balanceador administrado de la nube:** descartado por límites de ancho de banda o costo adicional; Caddy resuelve balanceo y HTTPS en un solo contenedor liviano.
- **Kubernetes o autoescalado:** descartados por exceso de complejidad y consumo de recursos para una VM pequeña.
- **Túneles locales (ngrok, Cloudflare Tunnel):** prohibidos por la letra.

## 4. Consecuencias
- **Ventajas:** tolerancia a la caída de una instancia de la API, despliegues sin corte de servicio, entorno reproducible desde cero en ≈ 8 min y distribución directa de actualizaciones de la app móvil.
- **Desventajas y cómo se mitigan:** al correr todo en una sola VM, la máquina sigue siendo un punto único de falla de hardware (mitigado con la reconstrucción automatizada por Terraform). Además, la carpeta `descargas` vive solo en la VM (si se reconstruye hay que volver a correr `scripts/publicar-apk.ps1`), y queda pendiente pasar las contraseñas del keystore de firma de la app móvil a variables de entorno fuera del `.csproj`.
