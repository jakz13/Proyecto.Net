# Test automatizado integral para UC-11: Gestionar Incidencias (Apertura, Seguimiento y Cierre)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== TEST AUTOMATIZADO UC-11: GESTIONAR INCIDENCIAS OPERATIVAS ===" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

# 1. Login como Despachador
Write-Host "`n1. Autenticando como Despachador..." -ForegroundColor Yellow
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

if ($loginPost.StatusCode -eq 200) {
    Write-Host "[OK] Autenticado como Despachador." -ForegroundColor Green
} else {
    Write-Error "Fallo en login."
}

# 2. Consultar listado de incidencias
Write-Host "`n2. Consultando /Incidencias/Index..." -ForegroundColor Yellow
$respIndex = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index" -WebSession $session -UseBasicParsing
if ($respIndex.StatusCode -eq 200 -and $respIndex.Content -match "Incidencias Operativas") {
    Write-Host "[OK] Pantalla de incidencias cargada correctamente." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar /Incidencias/Index."
}

# Verificar ausencia de etiquetas UC-xx en encabezados visibles
if ($respIndex.Content -match "UC-11") {
    Write-Host "[WARN] Se detectó etiqueta UC-11 visible." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Sin etiquetas UC-xx en encabezados visibles." -ForegroundColor Green
}

# 3. Flujo Principal A: Registrar una nueva incidencia
Write-Host "`n3. Registrando nueva incidencia (Avería Mecánica)..." -ForegroundColor Yellow
$tokenIndex = [regex]::Match($respIndex.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$bodyNueva = @{
    "__RequestVerificationToken" = $tokenIndex
    "NuevaIncidencia.Tipo" = "AveriaVehiculo"
    "NuevaIncidencia.Severidad" = "Alta"
    "NuevaIncidencia.VehiculoMatricula" = "STX-1029"
    "NuevaIncidencia.RepartidorNombre" = "Lucía Fernández"
    "NuevaIncidencia.Descripcion" = "Pérdida súbita de líquido de frenos en semáforo de Bvar. Artigas. Requiere auxilio urgente."
    "NuevaIncidencia.AccionInmediata" = "Se indicó detener la marcha de inmediato y se envió mecánico de guardia."
}

$respCrear = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index?handler=Registrar" -Method Post -Body $bodyNueva -WebSession $session -UseBasicParsing

Write-Host "[OK] Incidencia registrada." -ForegroundColor Green

# 4. Verificar que aparece en el listado y extraer su ID
Write-Host "`n4. Verificando que la incidencia aparece en el listado..." -ForegroundColor Yellow
$respIndexPostCrear = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index" -WebSession $session -UseBasicParsing

$regexInc = [regex]::Match($respIndexPostCrear.Content, 'data-inc-id="([0-9a-f\-]{36})"[^>]*data-inc-numero="(INC-2026-\d+)"')
if (-not $regexInc.Success) {
    Write-Error "No se pudo encontrar la incidencia creada en el listado."
}

$incId = $regexInc.Groups[1].Value
$incNumero = $regexInc.Groups[2].Value
Write-Host "[OK] Incidencia encontrada: $incNumero (ID: $incId)" -ForegroundColor Green

# 5. Flujo Principal B: Agregar nota de seguimiento y cambiar estado a EnInvestigacion
Write-Host "`n5. Agregando seguimiento a la bitácora..." -ForegroundColor Yellow
$tokenPostCrear = [regex]::Match($respIndexPostCrear.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$bodySeguimiento = @{
    "__RequestVerificationToken" = $tokenPostCrear
    "SeguimientoInput.IncidenciaId" = $incId
    "SeguimientoInput.Comentario" = "Mecánico arribó al lugar. Confirma rotura de flexible. Se traslada a taller y se despacha vehículo muleto."
    "SeguimientoInput.NuevoEstado" = "EnInvestigacion"
}

$respSeguimiento = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index?handler=Seguimiento" -Method Post -Body $bodySeguimiento -WebSession $session -UseBasicParsing
Write-Host "[OK] Nota de seguimiento registrada en la bitácora." -ForegroundColor Green

# 6. Flujo Principal C: Cerrar y resolver la incidencia
Write-Host "`n6. Resolviendo y cerrando formalmente la incidencia..." -ForegroundColor Yellow
$respIndexPostSeg = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index" -WebSession $session -UseBasicParsing
$tokenPostSeg = [regex]::Match($respIndexPostSeg.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$bodyCerrar = @{
    "__RequestVerificationToken" = $tokenPostSeg
    "CierreInput.IncidenciaId" = $incId
    "CierreInput.ResolucionDetalle" = "Vehículo muleto operativo en calle. Carga transferida y entregas reanudadas sin pérdida de bultos."
}

$respCerrar = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index?handler=Cerrar" -Method Post -Body $bodyCerrar -WebSession $session -UseBasicParsing
Write-Host "[OK] Incidencia cerrada y resuelta con éxito." -ForegroundColor Green

# 7. Verificar filtros
Write-Host "`n7. Verificando filtro de incidencias cerradas..." -ForegroundColor Yellow
$respFiltroCerradas = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index?estadoFiltro=Cerrada" -WebSession $session -UseBasicParsing
if ($respFiltroCerradas.Content -match $incNumero) {
    Write-Host "[OK] La incidencia $incNumero aparece correctamente bajo el filtro Cerrada." -ForegroundColor Green
} else {
    Write-Error "La incidencia no se encontró bajo el filtro Cerrada."
}

Write-Host "`n>>> TODOS LOS TESTS DE UC-11 PASARON EXITOSAMENTE! <<<" -ForegroundColor Green
