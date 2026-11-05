<#
.SYNOPSIS
    Demuestra el identificador de correlación de extremo a extremo (letra §6.12).

.DESCRIPTION
    Da de alta un envío como comercio y lo recibe en depósito, enviando un X-Correlation-Id propio en
    ambas llamadas. Luego muestra ese mismo identificador en los logs de la API y en los del Worker
    (el evento viaja por RabbitMQ). Cada ejecución crea un envío nuevo de demostración.

    Requisitos: clave SSH de la VM (la misma que usa desplegar.ps1).

.EXAMPLE
    .\scripts\demo-correlacion.ps1
    .\scripts\demo-correlacion.ps1 -Correlacion "defensa-001"
#>
param(
    [string]$Vm = $(if ($env:DESPLIEGUE_VM) { $env:DESPLIEGUE_VM } else { '152.70.219.103' }),
    [string]$Clave = $(if ($env:DESPLIEGUE_CLAVE_SSH) { $env:DESPLIEGUE_CLAVE_SSH } else { "$HOME\.ssh\oracle_ultimamilla" }),
    [string]$Dominio = 'shippingbox.duckdns.org',
    [string]$Correlacion = "demo-$(Get-Date -Format 'HHmmss')"
)

$ErrorActionPreference = 'Stop'
$destino = "ubuntu@$Vm"
$ssh = @('-i', $Clave, '-o', 'ConnectTimeout=20', '-o', 'StrictHostKeyChecking=accept-new')

function Paso($t) { Write-Host "`n==> $t" -ForegroundColor Cyan }

Paso 'Iniciando sesión como comercio y dando de alta un envío nuevo'
$cab = @{ 'X-Correlation-Id' = $Correlacion }
$loginComercio = Invoke-RestMethod -Method Post -Uri "https://$Dominio/api/v1/auth/login" -ContentType 'application/json' `
    -Body (@{ email = 'comercio@gmail.com'; password = 'Comercio.2026!' } | ConvertTo-Json)
$alta = @{
    nombreDestinatario = 'Cliente de Demostración'; calle = 'Av. Italia'; numero = '3456'; ciudad = 'Montevideo'
    bultos = @(@{ peso = 1.5; alto = 20; ancho = 20; profundo = 20 })
}
$envio = Invoke-RestMethod -Method Post -Uri "https://$Dominio/api/envios" -ContentType 'application/json; charset=utf-8' `
    -Headers ($cab + @{ Authorization = "Bearer $($loginComercio.token)" }) `
    -Body ([Text.Encoding]::UTF8.GetBytes(($alta | ConvertTo-Json -Depth 4)))
Write-Host "Envío creado: $($envio.codigoSeguimiento) (estado $($envio.estado))"

Paso 'Iniciando sesión como operario de depósito'
$login = Invoke-RestMethod -Method Post -Uri "https://$Dominio/api/v1/auth/login" -ContentType 'application/json' `
    -Body (@{ email = 'deposito@gmail.com'; password = 'Deposito.2026!' } | ConvertTo-Json)

Paso "Recepcionando el envío con X-Correlation-Id: $Correlacion"
$cuerpo = ConvertTo-Json -InputObject @{ codigosBultosEscaneados = @($envio.bultos) }
$resp = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "https://$Dominio/api/deposito/envios/$($envio.id)/recepcion" `
    -Headers ($cab + @{ Authorization = "Bearer $($login.token)" }) `
    -ContentType 'application/json' -Body $cuerpo
Write-Host "HTTP $($resp.StatusCode)  |  X-Correlation-Id devuelto: $($resp.Headers['X-Correlation-Id'])"
Write-Host ($resp.Content | ConvertFrom-Json | Select-Object codigoSeguimiento, estado, mensaje | Format-List | Out-String)

Paso 'Esperando a que el Worker procese el evento (20 s)'
Start-Sleep 20

Paso "Rastro de '$Correlacion' en los logs de la API"
ssh @ssh $destino "for c in api1 api2; do docker logs shippingbox-`$c-1 2>&1 | grep '$Correlacion' | head -2 | cut -c1-200; done"

Paso "Rastro de '$Correlacion' en los logs del Worker (el evento pasó por RabbitMQ)"
ssh @ssh $destino "docker logs shippingbox-worker-1 2>&1 | grep '$Correlacion' | head -2 | cut -c1-300"
