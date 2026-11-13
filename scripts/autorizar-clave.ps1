<#
.SYNOPSIS
    Autoriza en la VM de Azure la clave SSH pública de un compañero, para que pueda desplegar.

.DESCRIPTION
    Lo ejecuta quien ya tiene acceso a la VM. El compañero corre scripts\desplegar.bat: si no tiene clave se le
    crea una y el script le muestra su línea pública. Esa línea se pasa a este script.
    Solo se acepta una línea con formato de clave pública SSH (ssh-ed25519 / ssh-rsa / ecdsa), para no ejecutar nada raro en la VM.

.EXAMPLE
    .\scripts\autorizar-clave.ps1 -Publica "ssh-ed25519 AAAAC3Nza... despliegue-juan@PC-JUAN"
    .\scripts\autorizar-clave.ps1 -Archivo C:\temp\clave_juan.pub
#>
param(
    [string]$Publica,
    [string]$Archivo,

    [string]$Vm = $(if ($env:DESPLIEGUE_VM) { $env:DESPLIEGUE_VM } else { '57.156.57.234' }),
    [string]$Clave = $(if ($env:DESPLIEGUE_CLAVE_SSH) { $env:DESPLIEGUE_CLAVE_SSH } else { "$HOME\.ssh\azure_ultimamilla" })
)

$ErrorActionPreference = 'Stop'

if (-not $Publica -and $Archivo) { $Publica = (Get-Content $Archivo -Raw) }
if (-not $Publica) { throw 'Indicá la clave pública con -Publica "<línea>" o -Archivo <ruta al .pub>.' }
$Publica = $Publica.Trim()

# Una sola línea, con el formato de una clave pública SSH y sin caracteres que el shell pueda interpretar
if ($Publica -notmatch '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521)) [A-Za-z0-9+/=]+( [A-Za-z0-9._@:\-]+)?$') {
    throw 'La clave no tiene formato de clave pública SSH (una sola línea: "ssh-ed25519 AAAA... comentario"). No se autoriza.'
}
if (-not (Test-Path $Clave)) { throw "No se encuentra tu clave SSH en '$Clave': hace falta una clave ya autorizada en la VM para agregar otra." }

# Se envía en base64 para evitar problemas de comillas; se agrega solo si todavía no está
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Publica))
$remoto = 'k=$(echo ' + $b64 + ' | base64 -d); mkdir -p ~/.ssh; touch ~/.ssh/authorized_keys; chmod 600 ~/.ssh/authorized_keys; grep -qxF "$k" ~/.ssh/authorized_keys && echo "Esa clave ya estaba autorizada." || { echo "$k" >> ~/.ssh/authorized_keys; echo "Clave autorizada."; }'
ssh -i $Clave -o ConnectTimeout=20 -o StrictHostKeyChecking=accept-new "azureuser@$Vm" $remoto
if ($LASTEXITCODE -ne 0) { throw "No se pudo autorizar la clave (código $LASTEXITCODE)." }
