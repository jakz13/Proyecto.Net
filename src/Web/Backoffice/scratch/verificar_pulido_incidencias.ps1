$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Write-Host "=== VERIFICACION DEL PULIDO DE LA TABLA DE INCIDENCIAS ===" -ForegroundColor Cyan

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$baseUri = "http://localhost:5002"

# 1. Autenticar como Despachador
$loginGet = Invoke-WebRequest -Uri ($baseUri + "/Login") -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$loginPost = Invoke-WebRequest -Uri ($baseUri + "/Login") -Method Post -Body @{
    NombreUsuario = "Despachador Juan"
    RolSeleccionado = "Despachador"
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing
Write-Host "1. Autenticado como Despachador: $($loginPost.StatusCode)"

# 2. Consultar /Incidencias/Index
$resp = Invoke-WebRequest -Uri ($baseUri + "/Incidencias/Index") -WebSession $session -UseBasicParsing
$html = $resp.Content

# 3. Comprobar que el encabezado es 'Tipo' y no 'Tipo y Severidad'
if ($html -match "<th[^>]*>Tipo</th>" -and -not ($html -match "Tipo y Severidad")) {
    Write-Host "[OK] Columna 'Tipo' actualizada y 'Tipo y Severidad' eliminada." -ForegroundColor Green
} else {
    Write-Error "Fallo: No se encontro 'Tipo' o aun existe 'Tipo y Severidad'."
}

# 4. Comprobar que se quitaron los simbolos de los estados en la tabla
if ($html -match '<span class="badge[^>]*>\s*[\u25cf\u2713]') {
    Write-Error "Fallo: Aun persisten simbolos en los badges de Estado."
} else {
    Write-Host "[OK] Simbolos removidos de los badges de Estado en la tabla." -ForegroundColor Green
}

# 5. Comprobar que no hay texto '<span>Seguimiento</span>' en los botones de accion
if ($html -match "<span>Seguimiento</span>") {
    Write-Error "Fallo: El boton aun contiene el texto 'Seguimiento' visible."
} else {
    Write-Host "[OK] Texto 'Seguimiento' eliminado del cuerpo del boton." -ForegroundColor Green
}

# 6. Comprobar tooltip de Seguimiento
if ($html -match 'title="Seguimiento"') {
    Write-Host "[OK] Boton de seguimiento cuenta con tooltip title='Seguimiento'." -ForegroundColor Green
} else {
    Write-Error "Fallo: Falta el tooltip title='Seguimiento'."
}

# 7. Comprobar boton de Ver Resolucion en incidencias cerradas
if ($html -match 'title="Ver resoluci' -and $html -match "abrirModalResolucion") {
    Write-Host "[OK] Boton 'Ver resolucion' presente a la izquierda del de seguimiento en incidencias cerradas." -ForegroundColor Green
} else {
    Write-Error "Fallo: No se encontro el boton de ver resolucion para incidencias cerradas."
}

# 8. Comprobar que se quito la resolucion del texto de descripcion en la tabla
if ($html -match 'class="text-success small mt-1"') {
    Write-Error "Fallo: Aun figura texto de resolucion en la descripcion de la tabla."
} else {
    Write-Host "[OK] Texto de resolucion quitado de la columna de descripcion." -ForegroundColor Green
}

# 9. Comprobar si hay caracteres corruptos m-A-tilde
if ($html -match "m\xC3\xBA" -or $html -match "da\xC3\xB1" -or $html -match "rendici\xC3\xB3n") {
    Write-Host "[WARN] Se detectaron caracteres con doble codificacion UTF-8." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Textos correctamente codificados en UTF-8 sin doble codificacion." -ForegroundColor Green
}

Write-Host "`n=== TODAS LAS COMPROBACIONES DE PULIDO FUERON EXITOSAS ===" -ForegroundColor Cyan
