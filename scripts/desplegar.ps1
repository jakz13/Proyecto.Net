<#
.SYNOPSIS
    Despliega el proyecto en la VM de Oracle Cloud (shippingbox.duckdns.org).

.DESCRIPTION
    1. Construye las imágenes Docker en esta PC (la VM de 1 GB no puede compilar .NET).
    2. Las envía a la VM por SSH y copia docker-compose.prod.yml y el Caddyfile.
    3. Reinicia los servicios por turnos: api1, espera a que esté sana, y luego api2,
       así el balanceador mantiene el sitio disponible durante la actualización.
    4. Verifica https://<dominio>/ready.

    Requisitos: Docker Desktop, la clave SSH de la VM y que ~/shippingbox/.env.prod exista en la VM.

.EXAMPLE
    .\scripts\desplegar.ps1                       # api, worker y backoffice
    .\scripts\desplegar.ps1 -Servicios api        # solo la API (lo más rápido)
    .\scripts\desplegar.ps1 -Servicios backoffice
#>
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [ValidateSet('api', 'worker', 'backoffice')]
    [string[]]$Servicios = @('api', 'worker', 'backoffice'),

    [string]$Vm = $(if ($env:DESPLIEGUE_VM) { $env:DESPLIEGUE_VM } else { '152.70.219.103' }),
    [string]$Clave = $(if ($env:DESPLIEGUE_CLAVE_SSH) { $env:DESPLIEGUE_CLAVE_SSH } else { "$HOME\.ssh\oracle_ultimamilla" }),
    [string]$Dominio = 'shippingbox.duckdns.org'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

function Paso($texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Verificar($descripcion) { if ($LASTEXITCODE -ne 0) { throw "Falló: $descripcion (código $LASTEXITCODE)" } }

if (-not (Test-Path $Clave)) { throw "No se encuentra la clave SSH en '$Clave'. Indicá otra con -Clave o la variable DESPLIEGUE_CLAVE_SSH." }
$destino = "ubuntu@$Vm"
$opcionesSsh = @('-i', $Clave, '-o', 'ConnectTimeout=20', '-o', 'StrictHostKeyChecking=accept-new')

# ---------------------------------------------------------------- Docker
Paso 'Comprobando Docker'
function Test-DockerResponde {
    $trabajo = Start-Job { docker version --format '{{.Server.Version}}' 2>&1 }
    $listo = Wait-Job $trabajo -Timeout 15
    $salida = if ($listo) { (Receive-Job $trabajo) -join '' } else { '' }
    Remove-Job $trabajo -Force -ErrorAction SilentlyContinue
    return ($salida -match '^\d+\.\d+')
}
if (-not (Test-DockerResponde)) {
    $exe = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
    if (Test-Path $exe) {
        Write-Host 'Docker no responde: iniciando Docker Desktop (puede tardar un par de minutos)...'
        Start-Process $exe
        $espera = 0
        while (-not (Test-DockerResponde) -and $espera -lt 180) { Start-Sleep 10; $espera += 10 }
    }
    if (-not (Test-DockerResponde)) { throw 'Docker Desktop no responde. Reinicialo a mano y volvé a correr el script.' }
}
Write-Host 'Docker listo.'

# ---------------------------------------------------------------- Construcción
# api -> servicio de compose api1 (api1 y api2 comparten la misma imagen)
$mapaServicio = @{ api = 'api1'; worker = 'worker'; backoffice = 'backoffice' }
$serviciosCompose = $Servicios | ForEach-Object { $mapaServicio[$_] }
$imagenes = $Servicios | ForEach-Object { "ultimamilla/${_}:latest" }

Paso "Construyendo imágenes: $($Servicios -join ', ')"
# El compose exige estas variables al interpolarse; para construir sus valores no importan
$respaldo = @{}
foreach ($v in 'POSTGRES_PASSWORD', 'RABBITMQ_PASSWORD', 'JWT_SECRET', 'DOMINIO') { $respaldo[$v] = [Environment]::GetEnvironmentVariable($v) }
$env:POSTGRES_PASSWORD = 'x'; $env:RABBITMQ_PASSWORD = 'x'; $env:JWT_SECRET = 'x' * 40; $env:DOMINIO = 'x'
try {
    docker compose -f docker-compose.prod.yml build @serviciosCompose
    Verificar 'docker compose build'
}
finally {
    foreach ($v in $respaldo.Keys) { [Environment]::SetEnvironmentVariable($v, $respaldo[$v]) }
}

# ---------------------------------------------------------------- Envío
Paso 'Enviando archivos de configuración a la VM'
ssh @opcionesSsh $destino 'mkdir -p ~/shippingbox/docker'; Verificar 'ssh mkdir'
scp @opcionesSsh docker-compose.prod.yml "${destino}:shippingbox/docker-compose.prod.yml"; Verificar 'scp compose'
scp @opcionesSsh docker/Caddyfile.prod "${destino}:shippingbox/docker/Caddyfile.prod"; Verificar 'scp Caddyfile'

Paso 'Enviando imágenes a la VM (comprimidas; la primera vez son unos 350 MB)'
# El pipe se hace con cmd.exe: el de PowerShell 5 corrompe los datos binarios
$claveCmd = '"' + $Clave + '"'
cmd /c "docker save $($imagenes -join ' ') | ssh -C -i $claveCmd -o ConnectTimeout=20 $destino docker load"
Verificar 'docker save | ssh docker load'

# ---------------------------------------------------------------- Reinicio por turnos
Paso 'Reiniciando servicios en la VM'
$remoto = @'
set -e
cd ~/shippingbox
C="docker compose -f docker-compose.prod.yml --env-file .env.prod"
esperar() {
  for i in $(seq 1 36); do
    h=$(docker inspect -f '{{.State.Health.Status}}' "shippingbox-$1-1" 2>/dev/null || echo none)
    [ "$h" = healthy ] && { echo "  $1: sano"; return 0; }
    sleep 5
  done
  echo "ERROR: $1 no quedó sano"; docker logs --tail 30 "shippingbox-$1-1"; return 1
}
$C up -d --no-build --no-deps postgres redis rabbitmq caddy
for s in __SERVICIOS__; do
  case "$s" in
    api1) for n in api1 api2; do $C up -d --no-build --no-deps $n; esperar $n; done ;;
    *)    $C up -d --no-build --no-deps $s ;;
  esac
done
$C up -d --no-build
docker exec shippingbox-caddy-1 caddy reload --config /etc/caddy/Caddyfile >/dev/null 2>&1 || echo "  (aviso) Caddy no pudo recargar su configuración"
$C ps --format 'table {{.Service}}\t{{.Status}}'
'@
$remoto = $remoto.Replace('__SERVICIOS__', ($serviciosCompose -join ' ')).Replace("`r", '')
# Se envía en base64 para evitar problemas de saltos de línea entre Windows y Linux
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remoto))
ssh @opcionesSsh $destino "echo $b64 | base64 -d | bash"
Verificar 'reinicio en la VM'

# ---------------------------------------------------------------- Verificación
Paso "Verificando https://$Dominio/ready"
$codigo = 0
foreach ($intento in 1..12) {
    try { $codigo = (Invoke-WebRequest "https://$Dominio/ready" -UseBasicParsing -TimeoutSec 15).StatusCode }
    catch { $codigo = 0 }
    if ($codigo -eq 200) { break }
    Start-Sleep 5
}
if ($codigo -eq 200) {
    Write-Host "`nDespliegue terminado: https://$Dominio responde 200." -ForegroundColor Green
}
else {
    throw "El sitio no respondió 200 en /ready (último código: $codigo). Revisá los logs: ssh -i `"$Clave`" $destino 'cd shippingbox && docker compose -f docker-compose.prod.yml --env-file .env.prod logs --tail 50'"
}
