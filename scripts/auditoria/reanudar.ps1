# Reanuda la conversación de la auditoría si quedó frenada (sin cupo de tokens, sesión cerrada, etc.).
# Lo ejecuta el Programador de tareas cada 15 minutos. Solo actúa si: no hay otra reanudación en curso,
# la conversación lleva más de $umbralMin minutos sin escribirse y la auditoría no está marcada como terminada.
$sesion    = '70ebc7ed-eac9-44d4-99b2-f66f2414612d'
$proy      = 'C:\Users\Curbe\OneDrive\Escritorio\Proyecto\Proyecto.Net'
$jsonl     = "C:\Users\Curbe\.claude\projects\c--Users-Curbe-OneDrive-Escritorio-Proyecto-Proyecto-Net\$sesion.jsonl"
$diario    = "$proy\docs\auditoria\diario.log"
$log       = 'C:\Users\Curbe\auditoria_local\reanudar.log'
$limite    = Get-Date '2026-11-19T14:00:00'   # la defensa es el jueves 19/11 a las 15:00
$umbralMin = 12

function Anotar($t) { "$(Get-Date -Format s) $t" | Out-File $log -Append -Encoding utf8 }

if ((Get-Date) -gt $limite) { Anotar 'pasado el límite, no hago nada'; exit 0 }
if (Test-Path $diario) {
    if ((Get-Content $diario -Tail 1) -match 'FIN-AUDITORIA') { Anotar 'auditoría terminada'; exit 0 }
}
$inactivo = ((Get-Date) - (Get-Item $jsonl).LastWriteTime).TotalMinutes
if ($inactivo -lt $umbralMin) { exit 0 }   # la sesión está activa

# ¿Otra reanudación en curso?
$otra = Get-CimInstance Win32_Process -Filter "Name='node.exe' OR Name='claude.exe'" |
        Where-Object { $_.CommandLine -match '--resume' -and $_.CommandLine -match $sesion }
if ($otra) { Anotar 'ya hay una reanudación en curso'; exit 0 }

Anotar ("sesión inactiva {0:N0} min: reanudando" -f $inactivo)
Set-Location $proy
$prompt = 'Mensaje automático del script de reanudación (nadie escribió): la sesión quedó frenada. Retomá la auditoría nocturna: leé docs/auditoria/ESTADO.md, docs/auditoria/INFORME.md y la última línea de docs/auditoria/diario.log; si había una prueba EJECUTANDO sin TERMINADA, repetila; seguí con la siguiente ronda de pruebas bajo el mismo protocolo (pausa de ~5 min entre rondas, ideas nuevas en cada ronda, commits nuevos sin reescribir la historia anterior a d65d984). Al terminar tu tramo actualizá ESTADO.md con un resumen.'
$salida = & claude -p --resume $sesion --dangerously-skip-permissions $prompt 2>&1
Anotar ("terminó con código $LASTEXITCODE")
($salida | Select-Object -Last 15) | Out-File $log -Append -Encoding utf8
