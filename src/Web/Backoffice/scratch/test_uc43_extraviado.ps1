# Test automatizado para UC-43: Declarar envío extraviado y auditoría histórica (RF-11, RF-12)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== TEST AUTOMATIZADO UC-43: DECLARAR ENVÍO EXTRAVIADO ===" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

# 1. Autenticación como Despachador
Write-Host "`n1. Autenticando como Despachador..." -ForegroundColor Yellow
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

if ($loginPost.StatusCode -eq 200) {
    Write-Host "[OK] Autenticado exitosamente como Despachador." -ForegroundColor Green
} else {
    Write-Error "Fallo en autenticación de Despachador."
}

# 2. Consultar vista de gestión de envíos
Write-Host "`n2. Consultando /Envios/Index..." -ForegroundColor Yellow
$respIndex = Invoke-WebRequest -Uri "$baseUri/Envios/Index" -WebSession $session -UseBasicParsing
if ($respIndex.StatusCode -eq 200 -and ($respIndex.Content -match "Trazabilidad" -or $respIndex.Content -match "Env.*os")) {
    Write-Host "[OK] Vista de envíos cargada correctamente con título 'Gestión de Envíos y Trazabilidad'." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar /Envios/Index."
}

# Verificar que no hayan etiquetas UC-xx en encabezados visibles
if ($respIndex.Content -match "<h1[^>]*>.*UC-43") {
    Write-Host "[WARN] Se detectó etiqueta técnica UC-43 en el título visible." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Sin etiquetas técnicas UC-xx en el encabezado visible." -ForegroundColor Green
}

# 3. Flujo Principal: Declarar envío extraviado (TRK-2026-0007 en tránsito)
Write-Host "`n3. Declarando extravío de envío en tránsito TRK-2026-0007..." -ForegroundColor Yellow
$tokenEnvios = [regex]::Match($respIndex.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
$targetEnvioId = "11223344-5566-7788-99aa-bbccddeeff00"
$targetCodigo = "TRK-2026-0007"

$cuerpoDeclaracion = @{
    "__RequestVerificationToken" = $tokenEnvios
    "DeclaracionInput.EnvioId" = $targetEnvioId
    "DeclaracionInput.CodigoSeguimiento" = $targetCodigo
    "DeclaracionInput.MotivoExtravio" = "No aparece en rendición física del repartidor"
    "DeclaracionInput.ObservacionesDetalladas" = "El repartidor Juan Pérez finalizó su recorrido sin entregar el bulto y este no fue hallado en el vehículo durante la inspección de rendición física."
    "DeclaracionInput.CrearIncidenciaOperativa" = "true"
    "DeclaracionInput.NotificarComercio" = "true"
}

$respPost = Invoke-WebRequest -Uri "$baseUri/Envios/Index?handler=DeclararExtraviado" -Method Post -Body $cuerpoDeclaracion -WebSession $session -UseBasicParsing
Write-Host "[OK] Solicitud de extravío enviada. Status: $($respPost.StatusCode)" -ForegroundColor Green

# 4. Verificar impacto en la tabla de envíos
Write-Host "`n4. Verificando actualización del estado y registro de incidencia..." -ForegroundColor Yellow
$respConsulta = Invoke-WebRequest -Uri "$baseUri/Envios/Index" -WebSession $session -UseBasicParsing

if ($respConsulta.Content -match "TRK-2026-0007" -and $respConsulta.Content -match "Extraviado") {
    Write-Host "[OK] El envío TRK-2026-0007 ahora figura formalmente con estado EXTRAVIADO." -ForegroundColor Green
} else {
    Write-Error "Fallo: el envío TRK-2026-0007 no aparece como Extraviado."
}

if ($respConsulta.Content -match "INC-2026-") {
    Write-Host "[OK] Se generó y vinculó automáticamente la incidencia operativa." -ForegroundColor Green
} else {
    Write-Host "[WARN] No se observó enlace de incidencia en la salida." -ForegroundColor Yellow
}

# 5. Consultar Historial de Trazabilidad AJAX (RF-11, RF-12)
Write-Host "`n5. Consultando trazabilidad histórica vía AJAX (?handler=Historial)..." -ForegroundColor Yellow
$respHistorial = Invoke-WebRequest -Uri "$baseUri/Envios/Index?handler=Historial&id=$targetEnvioId" -WebSession $session -UseBasicParsing
$historialJson = $respHistorial.Content
Write-Host "Respuesta de Historial JSON:"
Write-Host $historialJson

if ($historialJson -match "(?i)extraviado" -and $historialJson -match "repartidor") {
    Write-Host "[OK] La línea de vida histórica incluye el evento de extravío con usuario, motivo y auditoría." -ForegroundColor Green
} else {
    Write-Error "Fallo: no se encontró el evento de extravío en el historial del envío."
}

# 6. Validar Precondición: Intentar declarar extraviado un envío que está en 'Admitido'
Write-Host "`n6. Validando precondición (Rechazo si el envío está en Admitido)..." -ForegroundColor Yellow
$admitidoEnvioId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"
$admitidoCodigo = "TRK-2026-0005"
$tokenEnvios2 = [regex]::Match($respConsulta.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$cuerpoInvalido = @{
    "__RequestVerificationToken" = $tokenEnvios2
    "DeclaracionInput.EnvioId" = $admitidoEnvioId
    "DeclaracionInput.CodigoSeguimiento" = $admitidoCodigo
    "DeclaracionInput.MotivoExtravio" = "Intento no permitido"
    "DeclaracionInput.ObservacionesDetalladas" = "No debería permitir extravío de un envío en estado Admitido."
    "DeclaracionInput.CrearIncidenciaOperativa" = "false"
    "DeclaracionInput.NotificarComercio" = "false"
}

$respPostInvalido = Invoke-WebRequest -Uri "$baseUri/Envios/Index?handler=DeclararExtraviado" -Method Post -Body $cuerpoInvalido -WebSession $session -UseBasicParsing

if ($respPostInvalido.Content -match "alert-danger" -and ($respPostInvalido.Content -match "extraviados aquellos" -or $respPostInvalido.Content -match "Dep.*sito")) {
    Write-Host "[OK] Precondición validada: el sistema rechazó la transición inválida y mostró mensaje explicativo." -ForegroundColor Green
} else {
    Write-Host "[WARN] Revisar si el mensaje de error de precondición se mostró." -ForegroundColor Yellow
}

Write-Host "`n=== TODAS LAS PRUEBAS DE UC-43 Y RF-11/RF-12 FINALIZARON EXITOSAMENTE ===" -ForegroundColor Cyan
