# Infraestructura como código en Azure (Terraform)

El ambiente de producción corre en **Azure** (desde el 11/11/2026; antes estuvo en Oracle Cloud, cuyo Terraform quedó en `infra/terraform/` como histórico). Este directorio define en código lo que ese ambiente necesita, para poder **destruirlo y reconstruirlo con comandos** (letra 6.14).

| Recurso | Nombre | Descripción |
|---|---|---|
| Grupo de recursos | `rg-ultimamilla-<ambiente>` | Todo vive aquí: borrar el grupo borra el ambiente |
| Red virtual y subred | `vnet-…`, `snet-…` | 10.10.0.0/16 y 10.10.1.0/24 |
| Reglas de red | `nsg-…` | Solo SSH (22), HTTP (80) y HTTPS (443 TCP y UDP/HTTP3). Base, cola, Redis y Prometheus **no** se publican |
| IP pública | `pip-…` | Estática (Standard): los tres dominios de DuckDNS apuntan a ella |
| Máquina virtual | `vm-…` | Ubuntu 24.04, `Standard_B2als_v2` (2 vCPU, 4 GB), disco de 64 GB, **solo clave SSH** |
| Preparación del servidor | `cloud-init.yaml.tftpl` | Docker, swap, carpeta de la aplicación y `.env.prod` con los secretos **generados al azar en la propia VM** |

Dos instancias de la API con balanceador: las levanta la aplicación con `docker-compose.prod.yml` (api1, api2 y Caddy) dentro de la VM; este código aprovisiona la infraestructura que las aloja.

## Secretos
Ninguno en el código ni en el repositorio:
- Autenticación contra Azure: la sesión de `az login`.
- Id de suscripción y claves **públicas** SSH: variables de entorno `TF_VAR_*` (ver `ambientes/prod.tfvars`).
- Contraseñas de la base, cola, JWT y Grafana: las genera `openssl rand` en la VM la primera vez y quedan en `~/shippingbox/.env.prod` (permisos 600).

## Requisitos
Terraform ≥ 1.5 (`winget install Hashicorp.Terraform`), Azure CLI con sesión (`az login --use-device-code`) y una clave SSH.

```powershell
$env:TF_VAR_subscription_id     = (az account show --query id -o tsv)
$env:TF_VAR_claves_publicas_ssh = (ConvertTo-Json @((Get-Content ~/.ssh/azure_ultimamilla.pub)))
```

## Operaciones
```powershell
cd infra\terraform-azure
terraform init
terraform plan    -var-file=ambientes/prod.tfvars     # planificar: no cambia nada
terraform apply   -var-file=ambientes/prod.tfvars     # crear o actualizar
terraform destroy -var-file=ambientes/prod.tfvars     # destruir todo el ambiente
```
Para un ambiente aparte (por ejemplo, ensayar la reconstrucción sin tocar producción) se copia `prod.tfvars` a `demo.tfvars` cambiando `ambiente = "demo"`: crea `rg-ultimamilla-demo` y no interfiere con el actual.

**Verificado el 12/11/2026:** `terraform validate` correcto y `terraform plan` contra la suscripción real: `Plan: 8 to add, 0 to change, 0 to destroy` (grupo, red, subred, NSG, IP, interfaz, asociación y VM). No se aplicó para no duplicar costo mientras el ambiente actual está en uso.

## Reconstruir el ambiente completo (escenario 7 de la defensa)
1. `terraform destroy` (o `terraform apply` en un ambiente nuevo) y anotar la salida `ip_publica`.
2. Apuntar los tres dominios de DuckDNS a esa IP.
3. Esperar ~3 minutos a que cloud-init termine (Docker, swap, `.env.prod`).
4. Desplegar la aplicación: `scripts\desplegar.bat` (construye las imágenes, las envía por SSH y levanta todo). El primer arranque ejecuta las migraciones y siembra los datos.
5. Verificar: `https://<dominio>/ready` responde 200.

## Estado actual (13/11/2026)
**El ambiente de producción está creado y gestionado por este Terraform** (`rg-ultimamilla-prod`). La VM anterior, armada a mano, se eliminó.
- Las imágenes las construye y publica el pipeline de GitHub en `ghcr.io/jakz13/ultimamilla/*` (paquetes públicos); la VM las **descarga**, no depende de la conexión de ninguna PC.
- `terraform.tfstate` queda **solo en la PC desde la que se aplica** (está fuera del repositorio): destruir y reconstruir hay que hacerlo desde ella.
- Reconstrucción medida: **≈ 2,5 minutos** de punta a punta (apply 30 s, cloud-init 40 s, descarga y arranque de la aplicación, DNS y verificación HTTPS).

## Reconstruir con UN comando
```powershell
$env:DUCKDNS_TOKEN = '<token de DuckDNS>'          # para apuntar los dominios a la IP nueva
$env:SEED_PASSWORD_DEMO = '<contraseña de las cuentas demo>'   # opcional; también SMTP_* si se quiere correo
.\scripts\reconstruir.ps1
```
Hace: `terraform apply` → actualiza DuckDNS → espera a la VM (cloud-init) → agrega los secretos opcionales a `.env.prod` → `desplegar.ps1 -DesdeRegistro` (la VM descarga las imágenes y levanta Docker Compose) → verifica `https://<dominio>/ready`.

## Guion para el escenario 7 de la defensa (destruir y reconstruir)
1. Mostrar el ambiente en pie: `https://shippingbox.duckdns.org/ready` y el grupo `rg-ultimamilla-prod` en el portal de Azure.
2. Destruir: `cd infra\terraform-azure; terraform destroy -var-file=ambientes/prod.tfvars` (≈ 25 s). Mostrar que el sitio deja de responder.
3. Reconstruir: `.\scripts\reconstruir.ps1` (≈ 2,5 min). Mientras corre se explica cada paso.
4. Verificar: `/ready` en 200, login en el Backoffice, una API detenida con el balanceador sirviendo.
Cuidado: Let's Encrypt limita a 5 certificados por semana para el mismo conjunto de dominios; cada reconstrucción pide uno nuevo. Ensayar 1 o 2 veces como máximo.

## Control de costo
Precios de lista consultados a la API pública de precios de Azure el 12/11/2026 (región `chilecentral`, USD):

| Recurso | Precio de lista | Por mes (730 h) |
|---|---|---|
| VM `Standard_B2als_v2` (Linux) | 0,0526 / hora | **38,4** |
| IP pública estática Standard | 0,005 / hora | **3,7** |
| Disco SSD estándar de 64 GB | ≈ 6 / mes (el de 32 GB cuesta 3,36) | **≈ 6** |
| Red virtual, NSG, grupo de recursos | sin costo | 0 |
| **Total estimado** | | **≈ 48 USD/mes ≈ 1,6 USD/día** |

La suscripción es "Azure for Students" con 100 USD de crédito. Con ese ritmo el ambiente permanente consume el crédito en ~2 meses, por eso la política es **destruir el ambiente cuando no se usa** (`terraform destroy`) y reconstruirlo para las demostraciones: un ambiente de ensayo de 4 horas cuesta ≈ 0,25 USD. El consumo real se consulta en el portal (Cost Management) o con `az consumption usage list`; esa consulta tarda un día en reflejar el uso y, al 12/11, todavía no devolvía filas.
