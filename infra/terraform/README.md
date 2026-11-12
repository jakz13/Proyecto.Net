# Infraestructura como código (Terraform) – Oracle Cloud (histórico: el ambiente actual está en Azure, ver `../terraform-azure/`)

Define todo lo necesario para correr la plataforma en Oracle Cloud (letra §6.14):

| Recurso | Archivo | Descripción |
|---|---|---|
| Red virtual, gateway, rutas, subred | `network.tf` | VCN con subred pública |
| Reglas de seguridad | `network.tf` | Solo SSH (22), HTTP (80) y HTTPS (443 TCP/UDP); las bases de datos, la cola y Prometheus no se publican |
| Máquina virtual | `compute.tf` | `VM.Standard.E2.1.Micro` (Always Free), Ubuntu 22.04 |
| IP pública reservada | `compute.tf` | Gratuita mientras esté asociada; evita cambiar DuckDNS al reiniciar |
| Preparación del servidor | `cloud-init.yaml.tftpl` | Docker, swap de 4 GB, firewall del sistema y `.env.prod` con secretos generados al azar **en la VM** |

Los **secretos no están en el código**: la autenticación con Oracle usa el token de sesión del navegador y las contraseñas de la aplicación se generan al azar en la VM durante el primer arranque.

## Requisitos

- Terraform ≥ 1.5 (`winget install Hashicorp.Terraform`).
- OCI CLI con sesión iniciada: `oci session authenticate --region sa-saopaulo-1 --profile-name DEFAULT` (el token vence en horas).
- Clave SSH `~/.ssh/oracle_ultimamilla` (y `.pub`).
- OCID del compartimento (en esta cuenta, el de la tenancy), por variable de entorno:
  `$env:TF_VAR_compartimento_ocid = "ocid1.tenancy.oc1..xxxx"`

## Operaciones

```powershell
cd infra\terraform
terraform init
terraform plan  -var-file=ambientes/prod.tfvars     # planificar (no cambia nada)
terraform apply -var-file=ambientes/prod.tfvars     # crear / actualizar
terraform destroy -var-file=ambientes/prod.tfvars   # destruir todo el ambiente
```

Variables por ambiente: copiar `ambientes/prod.tfvars` a `ambientes/demo.tfvars` y cambiar `ambiente`, `nombre_vm` y dominios.

## Reconstruir el ambiente completo con un comando (después de `apply`)

1. Apuntar los tres dominios de DuckDNS a la IP de la salida `ip_publica`.
2. Esperar ~3 minutos a que cloud-init termine (Docker, swap, `.env.prod`).
3. Desplegar la aplicación: `scripts\desplegar.bat` (construye las imágenes, las envía y levanta todo).

## Ambiente ya existente

La VM actual se creó antes con el CLI de Oracle. Para que Terraform la administre sin recrearla se
importan los recursos (`terraform import`) y se verifica con `terraform plan` que no haya cambios.
El `plan` puede mostrar diferencias menores (nombres, IP reservada nueva): revisarlas antes de aplicar.

## Costo

Todo lo definido entra en el nivel Always Free de Oracle (VM Micro, red y una IP reservada asociada): costo 0.
Dejar como evidencia una captura de *Billing → Cost Analysis* en la consola de Oracle (control de costo exigido por la letra).
