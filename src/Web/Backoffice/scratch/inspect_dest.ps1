$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -WebSession $session -UseBasicParsing
$token = ''
if ($loginGet.Content -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
    $token = $matches[1]
}
$null = Invoke-WebRequest -Uri 'http://localhost:5002/Login' -Method Post -Body @{ NombreUsuario='Despachador'; RolSeleccionado='Despachador'; __RequestVerificationToken=$token } -WebSession $session -UseBasicParsing

$rutaDestinoId = "aaaaaaaa-0003-0000-0000-000000000003"
$destGet = Invoke-WebRequest -Uri "http://localhost:5002/Rutas/Detalle/$rutaDestinoId" -WebSession $session -UseBasicParsing
$destHtml = [System.Text.Encoding]::UTF8.GetString($destGet.RawContentStream.ToArray())

Write-Host "Contains TRK-2026-0920?" ($destHtml.Contains("TRK-2026-0920"))
Write-Host "Paradas in HR-2026-0101:"
$regex = [regex]'TRK-2026-\d+'
$matches = $regex.Matches($destHtml)
foreach ($m in $matches) {
    Write-Host " - " $m.Value
}
