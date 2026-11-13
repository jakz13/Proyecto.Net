<#
.SYNOPSIS
    Despliega el proyecto en la VM de Azure (shippingbox.duckdns.org).

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
    .\scripts\desplegar.ps1 -Servicios portal
    .\scripts\desplegar.ps1 -Servicios seguimiento
#>
param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [ValidateSet('api', 'worker', 'backoffice', 'portal', 'seguimiento', 'panel')]
    [string[]]$Servicios = @('api', 'worker', 'backoffice', 'portal', 'seguimiento', 'panel'),

    [string]$Vm = $(if ($env:DESPLIEGUE_VM) { $env:DESPLIEGUE_VM } else { '57.156.65.113' }),
    [string]$Clave = $(if ($env:DESPLIEGUE_CLAVE_SSH) { $env:DESPLIEGUE_CLAVE_SSH } else { "$HOME\.ssh\azure_ultimamilla" }),
    [string]$Dominio = 'shippingbox.duckdns.org',
    [string]$DominioComercio = 'shippingbox-comercio.duckdns.org',
    [string]$DominioPanel = 'panel.shippingbox.duckdns.org',
    [string]$DominioSeguimiento = 'seguimiento.shippingbox.duckdns.org',

    # Las imágenes ya están en la VM (por ejemplo, copiadas desde otra VM de la misma región): no se vuelven a enviar
    [switch]$SinImagenes,
    # No verifica por dominio (útil mientras los dominios todavía apuntan a otra VM)
    [switch]$SinVerificacion
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

function Paso($texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Verificar($descripcion) { if ($LASTEXITCODE -ne 0) { throw "Falló: $descripcion (código $LASTEXITCODE)" } }

$destino = "azureuser@$Vm"

# ---------------------------------------------------------------- Clave SSH
# Cada persona usa su propia clave. Si no tiene, se crea aquí; para que la VM la acepte, quien administra
# la VM debe autorizar la parte pública con scripts\autorizar-clave.ps1 (la privada nunca sale de esta PC).
if (-not (Test-Path $Clave)) {
    Paso "No hay clave SSH en '$Clave': se crea una nueva para esta PC"
    New-Item -ItemType Directory -Force (Split-Path $Clave) | Out-Null
    ssh-keygen -t ed25519 -N '""' -f $Clave -C "despliegue-$env:USERNAME@$env:COMPUTERNAME" | Out-Null
    Verificar 'ssh-keygen'
}
$opcionesSsh = @('-i', $Clave, '-o', 'ConnectTimeout=20', '-o', 'StrictHostKeyChecking=accept-new')

# BatchMode: no pide contraseña; si la clave no está autorizada falla de inmediato
$previo = $ErrorActionPreference
$ErrorActionPreference = 'Continue'   # el rechazo de ssh sale por stderr y no debe cortar el script antes del aviso
ssh @opcionesSsh -o BatchMode=yes $destino 'true' 2>$null
$aceptada = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $previo
if (-not $aceptada) {
    $publica = if (Test-Path "$Clave.pub") { (Get-Content "$Clave.pub" -Raw).Trim() } else { '(no se encontró el archivo .pub)' }
    Write-Host "`nLa VM todavía no acepta esta clave SSH." -ForegroundColor Yellow
    Write-Host "Enviale esta línea (es la clave PÚBLICA, se puede compartir) a quien administra la VM:`n"
    Write-Host $publica -ForegroundColor Green
    Write-Host "`nEsa persona corre:  scripts\autorizar-clave.ps1 -Publica `"<la línea de arriba>`""
    Write-Host "Después volvé a ejecutar este despliegue."
    throw 'Clave SSH sin autorizar en la VM.'
}

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
# api -> servicio de compose api1; portal -> portal-comercio (api1 y api2 comparten la misma imagen)
$mapaServicio = @{ api = @('api1'); worker = @('worker'); backoffice = @('backoffice'); portal = @('portal-comercio'); seguimiento = @('seguimiento-publico'); panel = @('prometheus', 'grafana') }
$serviciosCompose = @($Servicios | ForEach-Object { $mapaServicio[$_] })
# Prometheus y Grafana usan imágenes públicas: no se construyen ni se envían desde esta PC
$construibles = @($serviciosCompose | Where-Object { $_ -notin 'prometheus', 'grafana' })
$imagenes = @($construibles | ForEach-Object { if ($_ -eq 'api1') { 'ultimamilla/api:latest' } else { "ultimamilla/${_}:latest" } })

Paso "Construyendo imágenes: $($construibles -join ', ')"
# El compose exige estas variables al interpolarse; para construir sus valores no importan
$respaldo = @{}
foreach ($v in 'POSTGRES_PASSWORD', 'RABBITMQ_PASSWORD', 'JWT_SECRET', 'DOMINIO', 'DOMINIO_COMERCIO', 'DOMINIO_PANEL', 'DOMINIO_SEGUIMIENTO', 'GRAFANA_PASSWORD') { $respaldo[$v] = [Environment]::GetEnvironmentVariable($v) }
$env:POSTGRES_PASSWORD = 'x'; $env:RABBITMQ_PASSWORD = 'x'; $env:JWT_SECRET = 'x' * 40; $env:DOMINIO = 'x'; $env:DOMINIO_COMERCIO = 'x'; $env:DOMINIO_PANEL = 'x'; $env:DOMINIO_SEGUIMIENTO = 'x'; $env:GRAFANA_PASSWORD = 'x'
try {
    if ($construibles.Count -gt 0) {
        foreach ($svc in $construibles) {
            Paso "Construyendo imagen para: $svc"
            docker compose -f docker-compose.prod.yml build $svc
            Verificar "docker compose build $svc"
        }
    }
}
finally {
    foreach ($v in $respaldo.Keys) { [Environment]::SetEnvironmentVariable($v, $respaldo[$v]) }
}

# ---------------------------------------------------------------- Envío
Paso 'Enviando archivos de configuración a la VM'
ssh @opcionesSsh $destino 'mkdir -p ~/shippingbox/docker'; Verificar 'ssh mkdir'
scp @opcionesSsh docker-compose.prod.yml "${destino}:shippingbox/docker-compose.prod.yml"; Verificar 'scp compose'
scp @opcionesSsh docker/Caddyfile.prod "${destino}:shippingbox/docker/Caddyfile.prod"; Verificar 'scp Caddyfile'
# Configuración de observabilidad y del plugin de métricas de RabbitMQ
foreach ($carpeta in 'prometheus', 'grafana', 'rabbitmq') {
    scp -r @opcionesSsh "docker/$carpeta" "${destino}:shippingbox/docker/"; Verificar "scp docker/$carpeta"
}

if ($imagenes.Count -gt 0 -and -not $SinImagenes) {
    Paso 'Enviando imágenes a la VM (comprimidas; la primera vez son unos 350 MB)'
    # El pipe se hace con cmd.exe: el de PowerShell 5 corrompe los datos binarios
    $claveCmd = '"' + $Clave + '"'
    cmd /c "docker save $($imagenes -join ' ') | ssh -C -i $claveCmd -o ConnectTimeout=20 $destino docker load"
    Verificar 'docker save | ssh docker load'
}

# ---------------------------------------------------------------- Reinicio por turnos
Paso 'Reiniciando servicios en la VM'
# La lógica de reinicio por turnos vive en scripts/reiniciar-remoto.sh (la comparte el pipeline de CI)
$remoto = (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'reiniciar-remoto.sh')).Replace("`r", '')
# Se envía en base64 para evitar problemas de saltos de línea entre Windows y Linux
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remoto))
ssh @opcionesSsh $destino "echo $b64 | base64 -d | bash -s -- '$($serviciosCompose -join ' ')'"
Verificar 'reinicio en la VM'

# ---------------------------------------------------------------- Verificación
function Esperar200($url) {
    $codigo = 0
    foreach ($intento in 1..12) {
        try { $codigo = (Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 15).StatusCode }
        catch { $codigo = 0 }
        if ($codigo -eq 200) { break }
        Start-Sleep 5
    }
    return $codigo
}
if ($SinVerificacion) { Write-Host "`nDespliegue terminado (sin verificación por dominio)." -ForegroundColor Green; return }
$sitios = @("https://$Dominio/ready")
if ($Servicios -contains 'portal') { $sitios += "https://$DominioComercio/" }
if ($Servicios -contains 'seguimiento') { $sitios += "https://$DominioSeguimiento/" }
if ($Servicios -contains 'panel') { $sitios += "https://$DominioPanel/api/health" }
foreach ($url in $sitios) {
    Paso "Verificando $url"
    $codigo = Esperar200 $url
    if ($codigo -ne 200) {
        throw "$url no respondió 200 (último código: $codigo). Revisá los logs: ssh -i `"$Clave`" $destino 'cd shippingbox && docker compose -f docker-compose.prod.yml --env-file .env.prod logs --tail 50'"
    }
    Write-Host "  $url responde 200." -ForegroundColor Green
}
Write-Host "`nDespliegue terminado." -ForegroundColor Green
