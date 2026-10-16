$baseUri = "http://localhost:5002"
$sesion = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# Login como Despachador
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -Method Get -WebSession $sesion -UseBasicParsing
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
$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body $loginBody -WebSession $sesion -UseBasicParsing

Write-Host "--- Comprobando Rutas/Index ---"
$htmlIndex = (Invoke-WebRequest -Uri "$baseUri/Rutas" -WebSession $sesion -UseBasicParsing).Content
$tieneJornada = $htmlIndex -match 'badge[^>]*>Jornada<'
$tienePorAsignar = $htmlIndex -match 'badge[^>]*>Por Asignar<'
Write-Host "Contiene badge Jornada: $tieneJornada"
Write-Host "Contiene badge Por Asignar: $tienePorAsignar"

Write-Host "--- Comprobando Rutas/Detalle/aaaaaaaa-0001-0000-0000-000000000001 ---"
$htmlDetalle = (Invoke-WebRequest -Uri "$baseUri/Rutas/Detalle/aaaaaaaa-0001-0000-0000-000000000001" -WebSession $sesion -UseBasicParsing).Content
$tieneIrDespacho = $htmlDetalle -match 'Ir a Despacho'
$tieneLiberadaRepartidor = $htmlDetalle -match 'Liberada para Repartidor'
$tieneViaje1 = $htmlDetalle -match 'Viaje 1 · Mañana'
$tieneAsignadoLiberada = $htmlDetalle -match 'Asignado a ruta \(Liberada\)'
$tieneModal = $htmlDetalle -match 'id="modalReasignarEnvio"'
$tieneBootstrapBundle = $htmlDetalle -match 'bootstrap\.bundle\.min\.js'

Write-Host "Contiene boton Ir a Despacho: $tieneIrDespacho"
Write-Host "Contiene badge Liberada para Repartidor: $tieneLiberadaRepartidor"
Write-Host "Contiene badge Viaje 1 en Detalle: $tieneViaje1"
Write-Host "Contiene Asignado a ruta (Liberada): $tieneAsignadoLiberada"
Write-Host "Existe modal id modalReasignarEnvio: $tieneModal"
Write-Host "Incluye bootstrap.bundle.min.js: $tieneBootstrapBundle"
$tieneContenedorFijo = $htmlDetalle -match 'contenedor-accion-fija'
$tieneAlturaFija = $htmlDetalle -match 'min-height:\s*470px'
$tienePanelDeposito = $htmlDetalle -match 'id="panelDeposito"'
Write-Host "Contiene contenedor-accion-fija: $tieneContenedorFijo"
Write-Host "Contiene modal body min-height 470px: $tieneAlturaFija"
Write-Host "Contiene panelDeposito: $tienePanelDeposito"
