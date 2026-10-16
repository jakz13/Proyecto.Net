$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -WebSession $session -UseBasicParsing
$token = ''
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
}
$null = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -Method Post -Body @{ NombreUsuario='Despachador'; RolSeleccionado='Despachador'; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing

$rutaIdLiberada = "aaaaaaaa-0001-0000-0000-000000000001"
$det = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/$rutaIdLiberada" -WebSession $session -UseBasicParsing
$tokenDetalle = ''
if ($det.Content -match '__RequestVerificationToken"[^>]*value="([^"]+)"') {
    $tokenDetalle = $matches[1]
}

$envioId2 = "99999999-0002-0000-0000-000000000002"
$reprogramarBody = @{
    "__RequestVerificationToken" = $tokenDetalle
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

$reprogramarPost = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/${rutaIdLiberada}?handler=Reasignar" -Method Post -Body $reprogramarBody -WebSession $session -UseBasicParsing
$reprogramarHtml = [System.Text.Encoding]::UTF8.GetString($reprogramarPost.RawContentStream.ToArray())

$regex = [regex]'<div class="alert[^"]*"[^>]*>([\s\S]*?)<\/div>'
$matches = $regex.Matches($reprogramarHtml)
foreach ($m in $matches) {
    Write-Host "ALERT FOUND: " $m.Groups[1].Value.Trim()
}
