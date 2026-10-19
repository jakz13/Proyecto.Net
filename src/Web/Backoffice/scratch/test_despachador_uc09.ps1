$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginUrl = 'http://localhost:5002/Login'

# 1. Login como Despachador
$loginGet = Invoke-WebRequest -Uri $loginUrl -WebSession $session -UseBasicParsing
$token = [regex]::Match($loginGet.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value

$respLogin = Invoke-WebRequest -Uri $loginUrl -Method Post -Body @{
    NombreUsuario = 'Juan Despachador'
    RolSeleccionado = 'Despachador'
    __RequestVerificationToken = $token
} -WebSession $session -UseBasicParsing

Write-Host "Login como Despachador Status: $($respLogin.StatusCode)"

# 2. Verificar que Despachador puede acceder a /Despacho/Index
$respDespacho = Invoke-WebRequest -Uri 'http://localhost:5002/Despacho/Index' -WebSession $session -UseBasicParsing
Write-Host "Acceso a /Despacho/Index Status: $($respDespacho.StatusCode)"
$tieneTitulo = $respDespacho.Content -match 'Control de Salida y Despacho'
Write-Host "Contiene Control de Salida y Despacho: $tieneTitulo"

# 3. Verificar que en _Sidebar aparece Control de Despacho
$tieneMenuDespacho = $respDespacho.Content -match 'Control de Despacho'
Write-Host "Sidebar contiene enlace Control de Despacho: $tieneMenuDespacho"

# 4. Verificar que en Rutas/Detalle para una ruta Liberada aparece el botón Despachar Ruta
$respDetalle = Invoke-WebRequest -Uri 'http://localhost:5002/Rutas/Detalle/aaaaaaaa-0001-0000-0000-000000000001' -WebSession $session -UseBasicParsing
$tieneBotonDespachar = $respDetalle.Content -match 'Despachar Ruta'
Write-Host "Detalle contiene boton Despachar Ruta: $tieneBotonDespachar"
