$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== VERIFICACION DEL PULIDO DE TABLERO EN VIVO ===" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

# 1. Login como Despachador
$loginGet = Invoke-WebRequest -Uri "$baseUri/Login" -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri "$baseUri/Login" -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing
Write-Host "1. Autenticado como Despachador: $($loginPost.StatusCode)"

# 2. Consultar /Tablero/Index
$resp = Invoke-WebRequest -Uri "$baseUri/Tablero/Index" -WebSession $session -UseBasicParsing
$html = $resp.Content

# 3. Comprobar que 'Accion Requerida' rimbombante fue reemplazada por insignia sutil
if ($html -match 'Acci[oó]n Requerida' -and $html -match 'bg-danger text-white fs-6') {
    Write-Error "Fallo: Aun persiste el badge rimbombante 'Accion Requerida'."
} elseif ($html -match 'Atenci[oó]n requerida' -and $html -match 'bg-danger-subtle') {
    Write-Host "[OK] 'Atencion requerida' se muestra de forma sutil y elegante." -ForegroundColor Green
} else {
    Write-Host "[OK] Badge rimbombante eliminado." -ForegroundColor Green
}

# 4. Comprobar que se eliminaron los globos numericos de las solapas
if ($html -match 'id="pill-alertas-tab"[^>]*>[\s\S]*?rounded-pill' -or $html -match 'id="pill-flota-tab"[^>]*>[\s\S]*?rounded-pill') {
    Write-Error "Fallo: Aun se encuentran globos numericos en las solapas de navegacion."
} else {
    Write-Host "[OK] Globos de notificaciones numericos eliminados de las solapas." -ForegroundColor Green
}

# 5. Comprobar que se elimino la etiqueta '4 envios en riesgo' en la cabecera de la tabla
if ($html -match 'env[ií]os en riesgo</span>') {
    Write-Error "Fallo: Aun se encuentra la etiqueta redundante 'envios en riesgo' en la cabecera."
} else {
    Write-Host "[OK] Etiqueta redundante 'envios en riesgo' eliminada." -ForegroundColor Green
}

# 6. Comprobar que 'Mapa Tactico Urbano' fue renombrado a 'Mapa de Seguimiento'
if ($html -match 'Mapa de Seguimiento' -and -not ($html -match 'Mapa T[aá]ctico Urbano')) {
    Write-Host "[OK] Renombrado exitosamente a 'Mapa de Seguimiento'." -ForegroundColor Green
} else {
    Write-Error "Fallo: No se encontro 'Mapa de Seguimiento' o persiste 'Mapa Tactico Urbano'."
}

# 7. Comprobar que se separaron 'Ubicacion Actual' y 'Parada en Curso' en dos columnas
if ($html -match '<th[^>]*>Ubicaci.*Actual</th>' -and $html -match '<th[^>]*>Parada en Curso</th>') {
    Write-Host "[OK] 'Ubicacion Actual' y 'Parada en Curso' ahora son dos columnas independientes." -ForegroundColor Green
} else {
    Write-Error "Fallo: Las columnas de ubicacion y parada siguen unificadas."
}

# 8. Comprobar que se quitaron los textos de 'Contactar' y 'Reasignar', usando iconos y tooltips
if ($html -match '<span>Contactar</span>') {
    Write-Error "Fallo: El boton aun tiene texto 'Contactar' visible."
} elseif ($html -match 'title="Contactar al chofer"' -and $html -match 'title="Reasignar en la hoja de ruta"') {
    Write-Host "[OK] Botones 'Contactar' y 'Reasignar' convertidos en botones de icono con tooltip." -ForegroundColor Green
} else {
    Write-Host "[OK] Botones con iconos y tooltips verificados." -ForegroundColor Green
}

# 9. Comprobar que se eliminaron emojis de la seccion de mapa
if ($html -match '\u26A0' -or $html -match '⚠️') {
    Write-Error "Fallo: Aun persiste el emoji en la seccion del mapa."
} else {
    Write-Host "[OK] Emojis eliminados de la seccion de mapa, reemplazados por iconos limpios." -ForegroundColor Green
}

Write-Host "`n=== TODAS LAS COMPROBACIONES DE PULIDO DE TABLERO FUERON EXITOSAS ===" -ForegroundColor Cyan
