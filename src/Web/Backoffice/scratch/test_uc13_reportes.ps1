# Test automatizado integral para UC-13: Consultar reportes de gestion (RF-30)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== TEST AUTOMATIZADO UC-13: CONSULTAR REPORTES DE GESTION ===" -ForegroundColor Cyan

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

# 2. Consultar Reporte por defecto (Cumplimiento por Zona)
Write-Host "`n2. Consultando /Reportes/Index (Cumplimiento por Zona)..." -ForegroundColor Yellow
$respZona = Invoke-WebRequest -Uri "$baseUri/Reportes/Index" -WebSession $session -UseBasicParsing
if ($respZona.StatusCode -eq 200 -and $respZona.Content -match "Reportes de Gesti") {
    Write-Host "[OK] Pagina de reportes cargada correctamente." -ForegroundColor Green
} else {
    Write-Error "Fallo al cargar /Reportes/Index."
}

# 3. Validar ausencia de etiquetas tecnicas UC-xx
Write-Host "`n3. Verificando que no haya etiquetas UC-xx en encabezados visibles..." -ForegroundColor Yellow
if ($respZona.Content -match "UC-13") {
    Write-Host "[WARN] Se detecto etiqueta UC-13 en HTML." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Sin etiquetas UC-xx en encabezados visibles." -ForegroundColor Green
}

# 4. Validar contenido del reporte de Cumplimiento por Zona
Write-Host "`n4. Validando datos del reporte de Cumplimiento por Zona..." -ForegroundColor Yellow
if ($respZona.Content -match "Pocitos" -and $respZona.Content -match "Centro" -and $respZona.Content -match "Efectividad") {
    Write-Host "[OK] Reporte de cumplimiento por zona muestra métricas de SLA y zonas." -ForegroundColor Green
} else {
    Write-Error "Faltan datos de zonas en el reporte."
}

# 5. Consultar Reporte de Cumplimiento por Repartidor
Write-Host "`n5. Consultando reporte por Repartidor..." -ForegroundColor Yellow
$respRepartidor = Invoke-WebRequest -Uri "$baseUri/Reportes/Index?tipoReporte=CumplimientoRepartidor" -WebSession $session -UseBasicParsing
if ($respRepartidor.Content -match "Juan P" -and $respRepartidor.Content -match "Carlos G") {
    Write-Host "[OK] Reporte por repartidor generado con exito." -ForegroundColor Green
} else {
    Write-Error "Faltan repartidores en el reporte."
}

# 6. Consultar Reporte de Motivos de No Entrega
Write-Host "`n6. Consultando reporte de Motivos de No Entrega..." -ForegroundColor Yellow
$respMotivos = Invoke-WebRequest -Uri "$baseUri/Reportes/Index?tipoReporte=MotivosNoEntrega" -WebSession $session -UseBasicParsing
if ($respMotivos.Content -match "M-AUS" -and $respMotivos.Content -match "Destinatario no responde") {
    Write-Host "[OK] Reporte de motivos de no entrega generado con exito." -ForegroundColor Green
} else {
    Write-Error "Faltan motivos de no entrega en el reporte."
}

# 7. Consultar Reporte de Volumen y Peso
Write-Host "`n7. Consultando reporte de Volumen de Carga..." -ForegroundColor Yellow
$respVolumen = Invoke-WebRequest -Uri "$baseUri/Reportes/Index?tipoReporte=VolumenPeriodo" -WebSession $session -UseBasicParsing
if ($respVolumen.Content -match "Gu.*as Despachadas" -or $respVolumen.Content -match "Bultos") {
    Write-Host "[OK] Reporte de volumen y peso generado con exito." -ForegroundColor Green
} else {
    Write-Error "Fallo en reporte de volumen."
}

# 8. Consultar Reporte de Liquidacion por Comercio
Write-Host "`n8. Consultando reporte de Liquidacion por Comercio..." -ForegroundColor Yellow
$respLiquidacion = Invoke-WebRequest -Uri "$baseUri/Reportes/Index?tipoReporte=LiquidacionComercio" -WebSession $session -UseBasicParsing
if ($respLiquidacion.Content -match "Farmashop" -and $respLiquidacion.Content -match "Total Facturado") {
    Write-Host "[OK] Reporte de liquidacion y facturacion generado con exito." -ForegroundColor Green
} else {
    Write-Error "Faltan datos de liquidacion por comercio."
}

# 9. Probar Exportacion CSV
Write-Host "`n9. Probando exportacion de reporte en CSV..." -ForegroundColor Yellow
$respCsv = Invoke-WebRequest -Uri "$baseUri/Reportes/Index?handler=ExportarCsv&tipoReporte=LiquidacionComercio" -WebSession $session -UseBasicParsing
if ($respCsv.StatusCode -eq 200 -and $respCsv.Headers["Content-Type"] -match "text/csv") {
    Write-Host "[OK] Archivo CSV descargado con exito. Tipo: $($respCsv.Headers['Content-Type'])" -ForegroundColor Green
    if ($respCsv.Content -match "Farmashop" -and $respCsv.Content -match "Total Facturado") {
        Write-Host "[OK] Contenido CSV estructurado correctamente." -ForegroundColor Green
    }
} else {
    Write-Error "Fallo en la exportacion CSV."
}

# 10. Validar que en Tablero ya no aparece el badge 'Operacion en Vivo'
Write-Host "`n10. Verificando que 'Operacion en Vivo' se elimino del Tablero..." -ForegroundColor Yellow
$respTablero = Invoke-WebRequest -Uri "$baseUri/Tablero/Index" -WebSession $session -UseBasicParsing
if ($respTablero.Content -match 'badge bg-success-subtle[^>]*>.*?Operaci.*n en Vivo') {
    Write-Error "El badge de 'Operacion en Vivo' todavia esta presente en Tablero."
} else {
    Write-Host "[OK] Badge 'Operacion en Vivo' removido correctamente del Tablero." -ForegroundColor Green
}

Write-Host "`n*** TODOS LOS TESTS DE UC-13 PASARON EXITOSAMENTE! ***" -ForegroundColor Green
