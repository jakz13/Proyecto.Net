$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Invoke-WebRequest -Uri 'http://localhost:5002/Login?handler=Rapido&rol=Despachador' -WebSession $s -UseBasicParsing | Out-Null
$resp = Invoke-WebRequest -Uri 'http://localhost:5002/Incidencias/Index' -WebSession $s -UseBasicParsing
$resp.Content | Set-Content -Path 'src/Web/Backoffice/scratch/dump_incidencias.html' -Encoding UTF8
Write-Host "Dump saved. Length: $($resp.Content.Length)"
