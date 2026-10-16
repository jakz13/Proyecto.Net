$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -WebSession $session -UseBasicParsing
$token = ''
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
}
$null = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -Method Post -Body @{ NombreUsuario='Despachador'; RolSeleccionado='Despachador'; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing

$det = Invoke-WebRequest -Uri 'http://localhost:5002/Rutas/Detalle/aaaaaaaa-0001-0000-0000-000000000001' -WebSession $session -UseBasicParsing
if ($det.Content -match '(<form[^>]+>)') {
    Write-Host "FORM TAG:" $matches[1]
} else {
    Write-Host "NO FORM FOUND"
}
