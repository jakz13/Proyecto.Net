<#
.SYNOPSIS
    Reconstruye TODO el ambiente con un solo comando: infraestructura (Terraform), DNS, aplicación y verificación.
.DESCRIPTION
    1. terraform apply        crea red, firewall, IP y VM (cloud-init instala Docker y deja los secretos en la VM)
    2. actualizar-dns.ps1     apunta los dominios a la IP nueva
    3. espera a la VM         (SSH disponible y cloud-init terminado)
    4. desplegar.ps1          construye las imágenes, las envía y levanta la aplicación (Docker Compose)
    5. verificación           https://<dominio>/ready responde 200
    Para destruir: terraform destroy -var-file=ambientes/prod.tfvars (desde infra\terraform-azure).
    Requisitos: az login, y las variables de entorno DUCKDNS_TOKEN (y opcionalmente TF_VAR_*, que se calculan si faltan).
.EXAMPLE
    $env:DUCKDNS_TOKEN = '<token>'; .\scripts\reconstruir.ps1
#>
param(
    [string]$Ambiente = 'prod',
    [string]$Clave = "$HOME\.ssh\azure_ultimamilla",
    [string]$Dominio = 'shippingbox.duckdns.org',
    [switch]$SinDespliegueDeAplicacion
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$tf = Join-Path $raiz 'infra\terraform-azure'
function Paso($t) { Write-Host "`n==> $t" -ForegroundColor Cyan }

if (-not $env:TF_VAR_subscription_id) { $env:TF_VAR_subscription_id = (az account show --query id -o tsv) }
if (-not $env:TF_VAR_claves_publicas_ssh) {
    $claves = @(Get-Content "$HOME\.ssh\azure_ultimamilla.pub"); if (Test-Path "$HOME\.ssh\ci_deploy.pub") { $claves += Get-Content "$HOME\.ssh\ci_deploy.pub" }
    $env:TF_VAR_claves_publicas_ssh = (ConvertTo-Json @($claves) -Compress)
}

Push-Location $tf
try {
    Paso 'Terraform: init y apply'
    terraform init -input=false | Out-Null
    terraform apply -input=false -auto-approve -var-file="ambientes/$Ambiente.tfvars"; if ($LASTEXITCODE) { throw 'terraform apply falló' }
    $ip = (terraform output -raw ip_publica).Trim()
} finally { Pop-Location }
Write-Host "IP pública de la VM nueva: $ip" -ForegroundColor Green

Paso 'DNS: apuntar los dominios a la IP nueva'
& (Join-Path $PSScriptRoot 'actualizar-dns.ps1') -Ip $ip

Paso 'Esperando a que la VM termine de prepararse (SSH + cloud-init)'
$opc = @('-i', $Clave, '-o', 'ConnectTimeout=10', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=yes')
$listo = $false
for ($i = 1; $i -le 60; $i++) {
    $r = ssh @opc "azureuser@$ip" 'cloud-init status 2>/dev/null; test -f ~/shippingbox/.env.prod && docker --version' 2>$null
    if ($LASTEXITCODE -eq 0 -and ($r -join ' ') -match 'done' -and ($r -join ' ') -match 'Docker') { $listo = $true; break }
    Write-Host "  intento $i/60 (la VM todavía se prepara)..." -ForegroundColor DarkGray; Start-Sleep -Seconds 10
}
if (-not $listo) { throw 'La VM no terminó de prepararse a tiempo.' }

# Secretos opcionales de la aplicación (no están en el repositorio ni en Terraform): se agregan a .env.prod de la VM si se definieron como variables de entorno
$extra = @()
foreach ($v in 'SEED_PASSWORD_DEMO', 'SMTP_HOST', 'SMTP_PORT', 'SMTP_SSL', 'SMTP_USER', 'SMTP_PASS', 'SMTP_FROM') {
    $valor = [Environment]::GetEnvironmentVariable($v); if ($valor) { $extra += "$v=$valor" }
}
if ($extra.Count) {
    Paso 'Secretos de la aplicación (SEED_PASSWORD_DEMO, SMTP) -> .env.prod de la VM'
    $texto = ($extra -join "`n") + "`n"
    $texto | ssh @opc "azureuser@$ip" 'grep -v -E "^(SEED_PASSWORD_DEMO|SMTP_[A-Z]+)=" ~/shippingbox/.env.prod > ~/shippingbox/.env.tmp; cat >> ~/shippingbox/.env.tmp; mv ~/shippingbox/.env.tmp ~/shippingbox/.env.prod; chmod 600 ~/shippingbox/.env.prod'
}

if (-not $SinDespliegueDeAplicacion) {
    Paso 'Aplicación: construir, enviar y levantar con Docker Compose'
    & (Join-Path $PSScriptRoot 'desplegar.ps1') -Vm $ip -Clave $Clave -DesdeRegistro
}

Paso "Verificación: https://$Dominio/ready"
$ok = $false
for ($i = 1; $i -le 30; $i++) {
    try { $c = (Invoke-WebRequest "https://$Dominio/ready" -UseBasicParsing -TimeoutSec 15).StatusCode; if ($c -eq 200) { $ok = $true; break } } catch { }
    Start-Sleep -Seconds 10
}
if ($ok) { Write-Host "Ambiente reconstruido y operativo: https://$Dominio" -ForegroundColor Green } else { throw "https://$Dominio/ready no respondió 200 (¿el certificado tarda?)" }
