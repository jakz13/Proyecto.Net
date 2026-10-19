$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri "http://localhost:5002/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "http://localhost:5002/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

try {
    $resp = Invoke-WebRequest -Uri "http://localhost:5002/Incidencias" -WebSession $session -UseBasicParsing
    Write-Host "URI: http://localhost:5002/Incidencias"
    Write-Host "StatusCode: $($resp.StatusCode)"
    Write-Host "Title: $([regex]::Match($resp.Content, '<title>(.*?)</title>').Groups[1].Value)"
    Write-Host "H1/H2: $([regex]::Match($resp.Content, '<h[12][^>]*>(.*?)</h[12]>').Groups[1].Value)"
    Write-Host "Content preview: $($resp.Content.Substring(0, [Math]::Min(500, $resp.Content.Length)))"
} catch {
    Write-Host "Error accessing /Incidencias: $_"
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        Write-Host "Response Body: $body"
    }
}

try {
    $resp2 = Invoke-WebRequest -Uri "http://localhost:5002/Incidencias/Index" -WebSession $session -UseBasicParsing
    Write-Host "URI: http://localhost:5002/Incidencias/Index"
    Write-Host "StatusCode: $($resp2.StatusCode)"
} catch {
    Write-Host "Error accessing /Incidencias/Index: $_"
}
