<#
.SYNOPSIS
    Apunta los dominios de DuckDNS a la IP pública de la VM.
.DESCRIPTION
    Los dominios de la aplicación son subdominios de duckdns.org. DuckDNS resuelve cualquier subdominio de un nombre (por ejemplo
    seguimiento.shippingbox y panel.shippingbox) a la misma IP que ese nombre, así que alcanza con actualizar los nombres base.
    Después de actualizar espera a que los resolutores públicos devuelvan la IP nueva: Caddy necesita que el dominio ya apunte a la VM
    para pedir los certificados de Let's Encrypt.

    El token de la cuenta no va en el repositorio: se toma de la variable de entorno DUCKDNS_TOKEN.
.PARAMETER Ip
    IP pública de la VM nueva (la que muestra "terraform output ip_publica").
.PARAMETER Nombres
    Nombres base de DuckDNS (sin ".duckdns.org").
.EXAMPLE
    $env:DUCKDNS_TOKEN = '<token>'; .\scripts\actualizar-dns.ps1 -Ip 57.156.63.70
#>
param(
    [Parameter(Mandatory = $true)][string]$Ip,
    [string[]]$Nombres = @('shippingbox', 'shippingbox-comercio'),
    [int]$EsperaMaximaSeg = 300
)
$ErrorActionPreference = 'Stop'

if ($Ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') { throw "La IP '$Ip' no es una dirección IPv4 válida." }
$token = $env:DUCKDNS_TOKEN
if (-not $token) { throw 'Falta la variable de entorno DUCKDNS_TOKEN (token de la cuenta DuckDNS).' }

foreach ($nombre in $Nombres) {
    $url = "https://www.duckdns.org/update?domains=$nombre&token=$token&ip=$Ip"
    $respuesta = (Invoke-RestMethod -Uri $url -TimeoutSec 30).ToString().Trim()
    if ($respuesta -ne 'OK') { throw "DuckDNS rechazó la actualización de '$nombre' (respuesta: $respuesta). Revisá el token y el nombre." }
    Write-Host "  $nombre.duckdns.org -> $Ip (DuckDNS respondió OK)" -ForegroundColor Green
}

# Espera a que un resolutor público ya devuelva la IP nueva (el TTL de DuckDNS es de 60 segundos)
$dominiosAVerificar = @($Nombres | ForEach-Object { "$_.duckdns.org" }) + @('panel.shippingbox.duckdns.org', 'seguimiento.shippingbox.duckdns.org')
$limite = (Get-Date).AddSeconds($EsperaMaximaSeg)
$pendientes = $dominiosAVerificar
while ($pendientes.Count -gt 0 -and (Get-Date) -lt $limite) {
    $pendientes = @($pendientes | Where-Object {
        try { -not (Resolve-DnsName $_ -Type A -Server 8.8.8.8 -DnsOnly -ErrorAction Stop | Where-Object { $_.IPAddress -eq $Ip }) } catch { $true }
    })
    if ($pendientes.Count -gt 0) { Start-Sleep -Seconds 10 }
}
if ($pendientes.Count -gt 0) { throw "Después de $EsperaMaximaSeg s estos dominios todavía no resuelven a ${Ip}: $($pendientes -join ', ')" }
Write-Host "  DNS propagado: todos los dominios resuelven a $Ip" -ForegroundColor Green
