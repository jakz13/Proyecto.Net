$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

Write-Host "Login code: $($loginPost.StatusCode)"

try {
    $resp = Invoke-WebRequest -Uri "$baseUri/Envios/Index" -WebSession $session -UseBasicParsing
    Write-Host "Envios code: $($resp.StatusCode)"
    Write-Host "Title match: $($resp.Content -match 'Gestión de Envíos y Trazabilidad')"
    Write-Host "Sample Content:"
    Write-Host ($resp.Content.Substring(0, [Math]::Min(500, $resp.Content.Length)))
} catch {
    Write-Host "Error accessing /Envios/Index: $_"
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "Response body: $($reader.ReadToEnd())"
    }
}
