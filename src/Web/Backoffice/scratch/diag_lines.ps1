$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $s -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $s -UseBasicParsing

$resp = Invoke-WebRequest -Uri "$baseUri/Incidencias/Index" -WebSession $s -UseBasicParsing
$lines = $resp.Content -split "`n"
foreach ($line in $lines) {
    if ($line -match "Abierta" -or $line -match "Cerrada") {
        Write-Host "MATCH: $($line.Trim())"
    }
}
