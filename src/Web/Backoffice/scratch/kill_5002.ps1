$conns = Get-NetTCPConnection -LocalPort 5002 -ErrorAction SilentlyContinue
if ($conns) {
    $procIds = $conns | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $procIds) {
        Write-Host "Killing PID $procId on port 5002..."
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}

Get-Process -Name 'UltimaMilla.Backoffice' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Write-Host "Port 5002 freed."
