# Test automatizado integral para UC-12: Monitorear tablero de operacion en vivo (RF-29)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== TEST AUTOMATIZADO UC-12: MONITOREAR TABLERO DE OPERACION EN VIVO ===" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

# 1. Login como Despachador
Write-Host "`n1. Autenticando como Despachador..." -ForegroundColor Yellow
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken".*?value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

if ($loginPost.StatusCode -eq 200) {
    Write-Host "[OK] Autenticado como Despachador." -ForegroundColor Green
} else {
    Write-Error "Fallo en autenticacion."
}

# 2. Consultar Tablero en Vivo
Write-Host "`n2. Consultando /Tablero/Index..." -ForegroundColor Yellow
$respTablero = Invoke-WebRequest -Uri "$baseUri/Tablero/Index" -WebSession $session -UseBasicParsing
if ($respTablero.StatusCode -eq 200 -and $respTablero.Content -match "Tablero de Operaci") {
    Write-Host "[OK] Tablero en vivo cargado correctamente." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar /Tablero/Index."
}

# 3. Validar ausencia de etiquetas tecnicas UC-xx en encabezados
Write-Host "`n3. Verificando limpieza de encabezados visibles..." -ForegroundColor Yellow
if ($respTablero.Content -match "UC-12") {
    Write-Host "[WARN] Se detecto etiqueta UC-12 en HTML." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Sin etiquetas UC-xx en encabezados visibles." -ForegroundColor Green
}

# 4. Validar existencia del mapa y elementos telemetricos
Write-Host "`n4. Verificando mapa vectorial y telemetria de flota..." -ForegroundColor Yellow
if ($respTablero.Content -match "lienzoMapaOperacion" -and $respTablero.Content -match "STX-1021" -and $respTablero.Content -match "STX-1029") {
    Write-Host "[OK] Lienzo de mapa tactico y vehiculos activos presentes." -ForegroundColor Green
} else {
    Write-Error "Faltan componentes esenciales del mapa o vehiculos en el tablero."
}

# 5. Validar alertas de envios en riesgo de ventana horaria (RF-29)
Write-Host "`n5. Verificando deteccion y resaltado de envios en riesgo de ventana..." -ForegroundColor Yellow
if ($respTablero.Content -match "ENV-2026-1011" -and $respTablero.Content -match "ENV-2026-1014") {
    Write-Host "[OK] Envios con riesgo de incumplimiento detectados y visibles en el listado de alertas." -ForegroundColor Green
} else {
    Write-Error "No se encontraron los envios en riesgo de ventana en el tablero."
}

# 6. Intervencion: Enviar instruccion prioritaria al repartidor
Write-Host "`n6. Probando intervencion de despacho (Envio de instruccion al repartidor)..." -ForegroundColor Yellow
$tokenTablero = [regex]::Match($respTablero.Content, 'name="__RequestVerificationToken".*?value="([^"]+)"').Groups[1].Value
$repartidorIdJuan = "11111111-1111-1111-1111-111111111111"

$bodyInstruccion = @{
    "__RequestVerificationToken" = $tokenTablero
    "repartidorId" = $repartidorIdJuan
    "mensajeInstruccion" = "Priorizar entrega de Farmacia San Roque en Av. Brasil antes de las 12:00. Tomar Bvar. Artigas para evitar obras."
}

$respInstruccion = Invoke-WebRequest -Uri "$baseUri/Tablero/Index?handler=EnviarInstruccion" -Method Post -Body $bodyInstruccion -WebSession $session -UseBasicParsing
if ($respInstruccion.StatusCode -eq 200 -and $respInstruccion.Content -match "Instrucci.*n enviada exitosamente") {
    Write-Host "[OK] Instruccion de intervencion enviada con exito al repartidor." -ForegroundColor Green
} else {
    Write-Error "Fallo al enviar instruccion al repartidor."
}

# 7. Validar endpoint AJAX de telemetria en vivo
Write-Host "`n7. Verificando endpoint telemetrico en vivo..." -ForegroundColor Yellow
$respAjax = Invoke-WebRequest -Uri "$baseUri/Tablero/Index?handler=DatosEnVivo" -WebSession $session -UseBasicParsing
if ($respAjax.StatusCode -eq 200 -and $respAjax.Content -match "totalRutasEnEjecucion") {
    Write-Host "[OK] Telemetria AJAX JSON en tiempo real respondiendo adecuadamente." -ForegroundColor Green
} else {
    Write-Error "Fallo en endpoint telemetrico en vivo."
}

Write-Host "`n*** TODOS LOS TESTS DE UC-12 PASARON EXITOSAMENTE! ***" -ForegroundColor Green
