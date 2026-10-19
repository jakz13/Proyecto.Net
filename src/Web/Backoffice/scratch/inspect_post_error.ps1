$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -WebSession $session -UseBasicParsing
$token = ''
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
}
$null = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -Method Post -Body @{ NombreUsuario='Despachador'; RolSeleccionado='Despachador'; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing

$rutaId = "aaaaaaaa-0001-0000-0000-000000000001"
$det = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/$rutaId" -WebSession $session -UseBasicParsing
$tokenDetalle = ''
if ($det.Content -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDetalle = $matches[1]
}

$body = @{
    "__RequestVerificationToken" = $tokenDetalle
    "ReasignacionInput.HojaDeRutaActualId" = $rutaId
    "ReasignacionInput.EnvioId" = "99999999-0001-0000-0000-000000000001"
    "ReasignacionInput.CodigoSeguimiento" = "TRK-2026-0920"
    "ReasignacionInput.MotivoIncidencia" = "AveriaVehiculo"
    "ReasignacionInput.DetalleMotivo" = "Test"
    "ReasignacionInput.AccionDestino" = "VolverADeposito"
}

try {
    # Don't auto-follow redirects (MaximumRedirection = 0)
    $resp = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/$rutaId?handler=Reasignar" -Method Post -Body $body -WebSession $session -MaximumRedirection 0 -UseBasicParsing
    Write-Host "STATUS:" $resp.StatusCode
    Write-Host "LOCATION:" $resp.Headers["Location"]
} catch {
    $ex = $_.Exception
    if ($ex.Response) {
        Write-Host "ERROR STATUS:" $ex.Response.StatusCode
        Write-Host "LOCATION:" $ex.Response.Headers["Location"]
        $stream = $ex.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "BODY:" $reader.ReadToEnd().Substring(0, 300)
    } else {
        Write-Host "EX:" $ex.Message
    }
}
