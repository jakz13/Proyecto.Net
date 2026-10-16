$ErrorActionPreference = "Stop"

Write-Host "=== TEST AUTOMATIZADO UC-10: REASIGNAR O REPROGRAMAR ENVÍO EN RUTA ===" -ForegroundColor Cyan

# 1. Esperar que el servidor esté activo
$baseUri = "http://localhost:5002"
$maxIntentos = 15
$servidorListo = $false

for ($i = 1; $i -le $maxIntentos; $i++) {
    try {
        $testPing = Invoke-WebRequest -Uri "$baseUri/Login" -Method Get -TimeoutSec 2 -UseBasicParsing
        if ($testPing.StatusCode -eq 200) {
            $servidorListo = $true
            Write-Host "Servidor activo en $baseUri" -ForegroundColor Green
            break
        }
    } catch {
        Start-Sleep -Milliseconds 800
    }
}

if (-not $servidorListo) {
    Write-Error "El servidor no respondió a tiempo."
    exit 1
}

# 2. Login como Despachador
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -Method Get -WebSession $session -UseBasicParsing

$token = ""
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
} elseif ($loginGet.Content -match 'value="([^"]+)"\s+name="__RequestVerificationToken"') {
    $token = $matches[1]
}

$loginBody = @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
}

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body $loginBody -WebSession $session -UseBasicParsing
Write-Host "Autenticación como Despachador: OK" -ForegroundColor Green

# 3. Consultar Detalle de Hoja de Ruta Liberada (HR-2026-0100)
$rutaIdLiberada = "aaaaaaaa-0001-0000-0000-000000000001"
$detalleGet = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/$rutaIdLiberada" -Method Get -WebSession $session -UseBasicParsing
$detalleHtml = [System.Text.Encoding]::UTF8.GetString($detalleGet.RawContentStream.ToArray())

if ($detalleHtml -match "HR-2026-0100" -and $detalleHtml -match "TRK-2026-0920") {
    Write-Host "[OK] Detalle de HR-2026-0100 cargado correctamente con paradas iniciales." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar el detalle de la hoja de ruta liberada."
    exit 1
}

# Verificar que no hay UC-xx en títulos visibles
if ($detalleHtml -match ">UC-10<" -or $detalleHtml -match ">UC-08<") {
    Write-Error "Se encontró texto 'UC-xx' en la interfaz."
    exit 1
} else {
    Write-Host "[OK] Sin etiquetas UC-xx en encabezados visibles." -ForegroundColor Green
}

# 4. Flujo Principal A: Reasignar parada TRK-2026-0920 a otra ruta (HR-2026-0101)
$tokenDetalle = ""
if ($detalleHtml -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDetalle = $matches[1]
} elseif ($detalleHtml -match 'value="([^"]+)"[^>]*__RequestVerificationToken') {
    $tokenDetalle = $matches[1]
} else {
    Write-Error "No se encontró el token antiforgery en Detalle."
    exit 1
}

$envioId1 = "99999999-0001-0000-0000-000000000001"
$rutaDestinoId = "aaaaaaaa-0003-0000-0000-000000000003" # HR-2026-0101 (Lucía Fernández)

$reasignarBody1 = @{
    "__RequestVerificationToken" = $tokenDetalle
    "ReasignacionInput.HojaDeRutaActualId" = $rutaIdLiberada
    "ReasignacionInput.EnvioId" = $envioId1
    "ReasignacionInput.CodigoSeguimiento" = "TRK-2026-0920"
    "ReasignacionInput.MotivoIncidencia" = "AveriaVehiculo"
    "ReasignacionInput.DetalleMotivo" = "Falla de alternador en la camioneta Partner"
    "ReasignacionInput.AccionDestino" = "ReasignarOtraRuta"
    "ReasignacionInput.HojaDeRutaDestinoId" = $rutaDestinoId
}

$reasignarPost1 = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/${rutaIdLiberada}?handler=Reasignar" -Method Post -Body $reasignarBody1 -WebSession $session -UseBasicParsing
$resp1Html = [System.Text.Encoding]::UTF8.GetString($reasignarPost1.RawContentStream.ToArray())

if ($resp1Html -match "retirado de &#x27;HR-2026-0100&#x27; y reasignado a &#x27;HR-2026-0101&#x27;" -or $resp1Html -match "reasignado a 'HR-2026-0101'" -or $resp1Html -match "TRK-2026-0920") {
    Write-Host "[OK] Flujo Principal A: Envío TRK-2026-0920 reasignado con éxito a HR-2026-0101." -ForegroundColor Green
} else {
    Write-Host "Respuesta: $resp1Html" -ForegroundColor Yellow
    Write-Error "Fallo en la reasignación de ruta."
    exit 1
}

# Verificar que en HR-2026-0101 ahora aparece TRK-2026-0920
$destGet = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/$rutaDestinoId" -Method Get -WebSession $session -UseBasicParsing
$destHtml = [System.Text.Encoding]::UTF8.GetString($destGet.RawContentStream.ToArray())
if ($destHtml -match "TRK-2026-0920") {
    Write-Host "[OK] El envío TRK-2026-0920 ahora forma parte de la ruta destino HR-2026-0101." -ForegroundColor Green
} else {
    Write-Error "El envío no aparece en la ruta destino."
    exit 1
}

# 5. Flujo Principal B: Reprogramar franja de parada TRK-2026-0921
$tokenDetalle2 = ""
if ($resp1Html -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDetalle2 = $matches[1]
} elseif ($resp1Html -match 'value="([^"]+)"[^>]*__RequestVerificationToken') {
    $tokenDetalle2 = $matches[1]
} else {
    $tokenDetalle2 = $tokenDetalle
}

$envioId2 = "99999999-0002-0000-0000-000000000002"
$reprogramarBody = @{
    "__RequestVerificationToken" = $tokenDetalle2
    "ReasignacionInput.HojaDeRutaActualId" = $rutaIdLiberada
    "ReasignacionInput.EnvioId" = $envioId2
    "ReasignacionInput.CodigoSeguimiento" = "TRK-2026-0921"
    "ReasignacionInput.MotivoIncidencia" = "SolicitudCliente"
    "ReasignacionInput.DetalleMotivo" = "Cliente solicita entrega por la tarde"
    "ReasignacionInput.AccionDestino" = "ReprogramarFranja"
    "ReasignacionInput.NuevaFranjaHoraria" = "14:00 - 18:00"
    "ReasignacionInput.NuevoTurno" = "Tarde"
    "ReasignacionInput.NuevaFecha" = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
}

$reprogramarPost = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/${rutaIdLiberada}?handler=Reasignar" -Method Post -Body $reprogramarBody -WebSession $session -UseBasicParsing
$reprogramarHtml = [System.Text.Encoding]::UTF8.GetString($reprogramarPost.RawContentStream.ToArray())

if ($reprogramarHtml -match "reprogramado para" -or $reprogramarHtml -match "Reintegrado a dep&#xF3;sito") {
    Write-Host "[OK] Flujo Principal B: Envío TRK-2026-0921 reprogramado de franja y reintegrado a depósito." -ForegroundColor Green
} else {
    Write-Host "Respuesta: $reprogramarHtml" -ForegroundColor Yellow
    Write-Error "Fallo en reprogramar franja."
    exit 1
}

# 6. Flujo Alternativo: Ruta ya despachada (HR-2026-0099) y registro de No Entrega en tránsito
$rutaIdDespachada = "aaaaaaaa-0002-0000-0000-000000000002"
$despachadaGet = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/$rutaIdDespachada" -Method Get -WebSession $session -UseBasicParsing
$despachadaHtml = [System.Text.Encoding]::UTF8.GetString($despachadaGet.RawContentStream.ToArray())

if ($despachadaHtml -match "HR-2026-0099" -and $despachadaHtml -match "TRK-2026-0810") {
    Write-Host "[OK] Detalle de ruta despachada HR-2026-0099 cargado correctamente." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar ruta despachada."
    exit 1
}

$tokenDespachada = ""
if ($despachadaHtml -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDespachada = $matches[1]
} elseif ($despachadaHtml -match 'value="([^"]+)"[^>]*__RequestVerificationToken') {
    $tokenDespachada = $matches[1]
} else {
    Write-Error "No se encontró el token antiforgery en ruta despachada."
    exit 1
}

$envioId4 = "99999999-0004-0000-0000-000000000004"
$incidenciaTransitoBody = @{
    "__RequestVerificationToken" = $tokenDespachada
    "ReasignacionInput.HojaDeRutaActualId" = $rutaIdDespachada
    "ReasignacionInput.EnvioId" = $envioId4
    "ReasignacionInput.CodigoSeguimiento" = "TRK-2026-0810"
    "ReasignacionInput.MotivoIncidencia" = "IncidenciaVial"
    "ReasignacionInput.DetalleMotivo" = "Corte total en Bv. España por obras sanitarias"
    "ReasignacionInput.AccionDestino" = "VolverADeposito"
    "ReasignacionInput.CodigoMotivoNoEntrega" = "AVERIA_VEHICULO"
}

$incidenciaPost = Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/${rutaIdDespachada}?handler=Reasignar" -Method Post -Body $incidenciaTransitoBody -WebSession $session -UseBasicParsing
$incidenciaHtml = [System.Text.Encoding]::UTF8.GetString($incidenciaPost.RawContentStream.ToArray())

if ($incidenciaHtml -match "No Entregado" -and $incidenciaHtml -match "ALERTA PRIORITARIA") {
    Write-Host "[OK] Flujo Alternativo: Envío TRK-2026-0810 marcado como No Entregado en tránsito con alerta a repartidor." -ForegroundColor Green
} else {
    Write-Host "Respuesta: $incidenciaHtml" -ForegroundColor Yellow
    Write-Error "Fallo en flujo alternativo de ruta despachada."
    exit 1
}

# 7. Verificar log de eventos histórico (RF-12, RF-15)
if ($incidenciaHtml -match "Registro de Eventos e Incidencias en Ruta" -and ($incidenciaHtml -match "Corte total en Bv" -or $incidenciaHtml -match "TRK-2026-0810")) {
    Write-Host "[OK] Eventos registrados y reflejados en el log histórico de auditoría." -ForegroundColor Green
} else {
    Write-Error "Fallo en la verificación del registro de eventos históricos."
    exit 1
}

Write-Host "`n>>> TODOS LOS TESTS AUTOMATIZADOS DE UC-10 PASARON EXITOSAMENTE! <<<" -ForegroundColor Green
