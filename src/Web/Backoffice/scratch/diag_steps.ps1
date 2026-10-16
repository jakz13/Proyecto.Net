$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Write-Host "Step 1: GET /Login"
$loginGet = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -WebSession $session -UseBasicParsing
Write-Host "Login GET status: $($loginGet.StatusCode)"

$token = ''
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
}
Write-Host "Token length: $($token.Length)"

Write-Host "Step 2: POST /Login"
$loginPost = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -Method Post -Body @{ NombreUsuario='Despachador'; RolSeleccionado='Despachador'; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing
Write-Host "Login POST status: $($loginPost.StatusCode)"

$rutaId = "aaaaaaaa-0001-0000-0000-000000000001"
Write-Host "Step 3: GET /Rutas/Detalle/$rutaId"
$det = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/$rutaId" -WebSession $session -UseBasicParsing
Write-Host "Detalle GET status: $($det.StatusCode)"

$tokenDetalle = ''
if ($det.Content -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDetalle = $matches[1]
} elseif ($det.Content -match 'value="([^"]+)"[^>]*__RequestVerificationToken') {
    $tokenDetalle = $matches[1]
}
Write-Host "Detalle token length: $($tokenDetalle.Length)"

$body = @{
    "__RequestVerificationToken" = $tokenDetalle
    "ReasignacionInput.HojaDeRutaActualId" = $rutaId
    "ReasignacionInput.EnvioId" = "99999999-0001-0000-0000-000000000001"
    "ReasignacionInput.CodigoSeguimiento" = "TRK-2026-0920"
    "ReasignacionInput.MotivoIncidencia" = "AveriaVehiculo"
    "ReasignacionInput.DetalleMotivo" = "Test"
    "ReasignacionInput.AccionDestino" = "VolverADeposito"
}

Write-Host "Step 4: POST /Rutas/Detalle/${rutaId}?handler=Reasignar"
try {
    $resp = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/${rutaId}?handler=Reasignar" -Method Post -Body $body -WebSession $session -MaximumRedirection 0 -UseBasicParsing
    Write-Host "POST Success Status: $($resp.StatusCode)"
    Write-Host "Location header: $($resp.Headers['Location'])"
} catch {
    Write-Host "POST Exception: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Response Status: $($_.Exception.Response.StatusCode)"
    }
}
