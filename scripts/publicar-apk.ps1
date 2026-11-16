<#
.SYNOPSIS
    Publica el APK firmado de la app del repartidor en la VM de Azure para la actualizacion OTA (https://shippingbox.duckdns.org/descargas/).

.DESCRIPTION
    Es un paso EXPLICITO (nunca se ejecuta solo con un despliegue normal). Sirve los archivos Caddy desde ~/shippingbox/descargas; no toca la API,
    la base de datos ni los contenedores.

    1. Toma la version del propio APK (aapt2 del SDK de Android). Si no hay aapt2, la toma del .csproj y verifica que el APK sea mas nuevo que el .csproj.
    2. Rechaza el APK si su versionCode no es MAYOR que el publicado (Android no actualiza con el mismo numero). -Forzar lo permite.
    3. Calcula sha256 y tamano, y genera version.json (versionCode, versionName, apkUrl, sha256, tamano, notas).
    4. Sube el APK y version.json a archivos temporales, comprueba el sha256 en la VM y recien entonces los renombra (mv): version.json va ULTIMO,
       asi la app nunca ve una version cuyo APK todavia no esta completo.
    5. Verifica por HTTPS: version.json (200, Cache-Control no-store, versionCode correcto) y el APK (200, tipo MIME y Content-Length).

    Despues de reconstruir el ambiente con scripts\reconstruir.ps1 (escenario 7) la carpeta de la VM queda vacia: hay que volver a ejecutar este script.

.EXAMPLE
    .\scripts\publicar-apk.ps1
    .\scripts\publicar-apk.ps1 -Apk C:\ruta\UltimaMilla-Release-Signed.apk -Notas "Correccion del escaneo de bultos"
    .\scripts\desplegar.ps1 mobile        # equivalente (desplegar.bat mobile)
#>
param(
    [string]$Apk = '',
    [string]$Vm = $(if ($env:DESPLIEGUE_VM) { $env:DESPLIEGUE_VM } else { '57.156.63.70' }),
    [string]$Clave = $(if ($env:DESPLIEGUE_CLAVE_SSH) { $env:DESPLIEGUE_CLAVE_SSH } else { "$HOME\.ssh\azure_ultimamilla" }),
    [string]$Dominio = 'shippingbox.duckdns.org',
    [string]$Notas = 'Sincronizacion en tiempo real via SignalR y drenaje automatico de cola offline',
    [string]$PaqueteEsperado = 'com.ultimamilla.repartidor',
    # Permite publicar un versionCode igual o menor que el vigente (volver atras) o una version tomada del .csproj sin poder compararla con el APK
    [switch]$Forzar
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
if (-not $Apk) { $Apk = Join-Path $raiz 'artifacts\UltimaMilla-Release-Signed.apk' }
$nombreApk = 'UltimaMilla-Release-Signed.apk'
$csproj = Join-Path $raiz 'src\Mobile\UltimaMilla.Mobile\UltimaMilla.Mobile.csproj'
$destino = "azureuser@$Vm"
$opcionesSsh = @('-i', $Clave, '-o', 'ConnectTimeout=20', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=yes')

function Paso($texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }
function Verificar($descripcion) { if ($LASTEXITCODE -ne 0) { throw "Fallo: $descripcion (codigo $LASTEXITCODE)" } }

# ---------------------------------------------------------------- APK
if (-not (Test-Path $Apk)) { throw "No existe el APK: $Apk (compilalo y firmalo primero; artifacts\ no se versiona)." }
$archivo = Get-Item $Apk
if ($archivo.Length -lt 1MB) { throw "El APK pesa solo $($archivo.Length) bytes: parece incompleto." }

# ---------------------------------------------------------------- Version (del APK; si no se puede, del .csproj)
Paso 'Leyendo la version'
$versionCode = $null; $versionName = $null; $origen = ''
$sdk = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, "$env:LOCALAPPDATA\Android\Sdk", 'C:\Program Files (x86)\Android\android-sdk') | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
$aapt2 = $null
if ($sdk -and (Test-Path (Join-Path $sdk 'build-tools'))) {
    $aapt2 = Get-ChildItem (Join-Path $sdk 'build-tools') -Recurse -Filter 'aapt2.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
}
if ($aapt2) {
    $badging = & $aapt2.FullName dump badging $archivo.FullName | Select-Object -First 1
    if ($badging -match "package: name='([^']+)' versionCode='(\d+)' versionName='([^']*)'") {
        if ($Matches[1] -ne $PaqueteEsperado) { throw "El APK es del paquete '$($Matches[1])' y se esperaba '$PaqueteEsperado'." }
        $versionCode = [int]$Matches[2]; $versionName = $Matches[3]; $origen = 'del propio APK (aapt2)'
    }
}
if ($null -eq $versionCode) {
    # Sin aapt2 no se puede leer del APK: se usa el .csproj, que solo es fiable si el APK se compilo DESPUES de su ultima edicion
    $xml = [xml](Get-Content -Raw $csproj)
    $versionCode = [int]($xml.Project.PropertyGroup.ApplicationVersion | Where-Object { $_ } | Select-Object -First 1)
    $versionName = [string]($xml.Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1)
    $origen = 'del .csproj (no se encontro aapt2 para leerla del APK)'
    if ($archivo.LastWriteTime -lt (Get-Item $csproj).LastWriteTime -and -not $Forzar) {
        throw "El APK ($($archivo.LastWriteTime)) es mas viejo que el .csproj ($((Get-Item $csproj).LastWriteTime)): no se puede asegurar que su version sea $versionCode. Recompilalo, instala el SDK de Android (aapt2) o usa -Forzar."
    }
}
Write-Host "  versionCode=$versionCode versionName=$versionName  ($origen)"

# ---------------------------------------------------------------- Version ya publicada
Paso 'Comparando con la version publicada'
$publicado = $null
try { $publicado = Invoke-RestMethod "https://$Dominio/descargas/version.json?ts=$([guid]::NewGuid())" -TimeoutSec 20 } catch { Write-Host '  No hay version publicada todavia.' }
if ($publicado -and $publicado.versionCode) {
    Write-Host "  Publicada: versionCode=$($publicado.versionCode) versionName=$($publicado.versionName)"
    if ([int]$publicado.versionCode -ge $versionCode -and -not $Forzar) {
        throw "El versionCode $versionCode no es mayor que el publicado ($($publicado.versionCode)): Android no actualizaria la app. Sube ApplicationVersion en el .csproj, recompila, o usa -Forzar."
    }
}

# ---------------------------------------------------------------- version.json
$sha256 = (Get-FileHash $archivo.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$datos = [ordered]@{
    versionCode = $versionCode
    versionName = $versionName
    apkUrl      = "https://$Dominio/descargas/$nombreApk"
    sha256      = $sha256
    tamano      = $archivo.Length
    notas       = $Notas
}
$temporal = Join-Path ([IO.Path]::GetTempPath()) 'version.json'
# UTF-8 sin BOM: la app lo lee como JSON y un BOM lo invalidaria
[IO.File]::WriteAllText($temporal, ($datos | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))

# ---------------------------------------------------------------- Subida (temporales -> comprobacion -> mv; version.json al final)
Paso "Subiendo a la VM ($destino)"
ssh @opcionesSsh $destino 'mkdir -p ~/shippingbox/descargas'; Verificar 'ssh mkdir'
scp @opcionesSsh $archivo.FullName "${destino}:shippingbox/descargas/.$nombreApk.tmp"; Verificar 'scp apk'
scp @opcionesSsh $temporal "${destino}:shippingbox/descargas/.version.json.tmp"; Verificar 'scp version.json'
$remoto = (ssh @opcionesSsh $destino "sha256sum ~/shippingbox/descargas/.$nombreApk.tmp | cut -d' ' -f1").Trim()
if ($remoto -ne $sha256) {
    ssh @opcionesSsh $destino "rm -f ~/shippingbox/descargas/.$nombreApk.tmp ~/shippingbox/descargas/.version.json.tmp"
    throw "El sha256 del APK en la VM ($remoto) no coincide con el local ($sha256): la copia se corrompio. No se publico nada."
}
ssh @opcionesSsh $destino "cd ~/shippingbox/descargas && chmod 644 .$nombreApk.tmp .version.json.tmp && mv -f .$nombreApk.tmp $nombreApk && mv -f .version.json.tmp version.json"; Verificar 'ssh mv'

# ---------------------------------------------------------------- Verificacion por HTTPS
Paso 'Verificando por HTTPS'
$vj = Invoke-WebRequest "https://$Dominio/descargas/version.json?ts=$([guid]::NewGuid())" -UseBasicParsing -TimeoutSec 30
if ($vj.StatusCode -ne 200) { throw "version.json respondio $($vj.StatusCode)" }
if ($vj.Headers['Cache-Control'] -notmatch 'no-store') { throw "version.json sin Cache-Control no-store (llego: '$($vj.Headers['Cache-Control'])'). Revisa el Caddyfile." }
$leido = $vj.Content | ConvertFrom-Json
if ([int]$leido.versionCode -ne $versionCode) { throw "version.json publicado dice versionCode $($leido.versionCode) y se esperaba $versionCode." }
Write-Host "  version.json: 200, no-store, versionCode $($leido.versionCode)" -ForegroundColor Green

$cabecera = Invoke-WebRequest "https://$Dominio/descargas/$nombreApk" -Method Head -UseBasicParsing -TimeoutSec 30
if ($cabecera.StatusCode -ne 200) { throw "El APK respondio $($cabecera.StatusCode)" }
if ($cabecera.Headers['Content-Type'] -notmatch 'application/vnd.android.package-archive') { throw "El APK llego con tipo '$($cabecera.Headers['Content-Type'])'." }
if ([int64]($cabecera.Headers['Content-Length'] | Select-Object -First 1) -ne $archivo.Length) { throw 'El Content-Length del APK no coincide con el archivo subido.' }
Write-Host "  APK: 200, application/vnd.android.package-archive, $($archivo.Length) bytes" -ForegroundColor Green
Remove-Item $temporal -Force -ErrorAction SilentlyContinue
Write-Host "`nPublicado: https://$Dominio/descargas/$nombreApk  (version $versionName, codigo $versionCode)" -ForegroundColor Green
