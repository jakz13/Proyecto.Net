<#
.SYNOPSIS
  Prueba de punta a punta la API pública para comercios.

.DESCRIPTION
  Inicia sesión como comercio, obtiene la clave de pruebas y verifica: sandbox (200),
  clave inválida (401), pedido inválido (400) y límite de tasa (429).
  Con -ProbarReal además genera la clave real y verifica 201 y duplicado.
  ATENCIÓN: -ProbarReal REGENERA la clave real del comercio e invalida la anterior.

  La contraseña no se guarda en el repo: va por parámetro o por la variable ULTIMAMILLA_PASSWORD.

.EXAMPLE
  $env:ULTIMAMILLA_PASSWORD = '<contraseña>'
  .\scripts\probar-api-publica.ps1
  .\scripts\probar-api-publica.ps1 -BaseUrl http://localhost:5000 -ProbarReal
#>
param(
    [string]$BaseUrl = 'https://shippingbox.duckdns.org',
    [string]$Email = 'comercio@gmail.com',
    [string]$Password = $env:ULTIMAMILLA_PASSWORD,
    [switch]$ProbarReal,
    [int]$RafagaLimite = 25,
    [double]$Latitud = -34.916,
    [double]$Longitud = -56.180
)

if (-not $Password) { throw 'Falta la contraseña: use -Password o la variable ULTIMAMILLA_PASSWORD.' }
$BaseUrl = $BaseUrl.TrimEnd('/')
$fallos = 0

function Llamar([string]$Metodo, [string]$Ruta, $Cuerpo = $null, [hashtable]$Cabeceras = @{}) {
    $p = @{ Method = $Metodo; Uri = "$BaseUrl$Ruta"; Headers = $Cabeceras; ContentType = 'application/json' }
    if ($null -ne $Cuerpo) { $p.Body = ($Cuerpo | ConvertTo-Json -Depth 6) }
    # Los códigos de error (401, 400, 429…) lanzan excepción: se capturan para devolver el código
    try {
        $r = Invoke-WebRequest @p -UseBasicParsing
        return @{ Codigo = [int]$r.StatusCode; Json = ($r.Content | ConvertFrom-Json -ErrorAction SilentlyContinue) }
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $texto = ''
        try { $texto = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch {}
        return @{ Codigo = [int]$resp.StatusCode; Json = ($texto | ConvertFrom-Json -ErrorAction SilentlyContinue) }
    }
}

function Verificar([string]$Caso, [int]$Esperado, $Real, [scriptblock]$Extra = $null) {
    $ok = ($Real.Codigo -eq $Esperado) -and (($null -eq $Extra) -or (& $Extra $Real.Json))
    if ($ok) { Write-Host "[OK]    $Caso -> $($Real.Codigo)" -ForegroundColor Green }
    else { Write-Host "[FALLO] $Caso -> esperado $Esperado, recibido $($Real.Codigo)" -ForegroundColor Red; $script:fallos++ }
}

function NuevoPedido([string]$Referencia) {
    @{
        modalidadServicio = 'Estandar'; nombreDestinatario = 'Ana'; calle = 'Av. Italia'; numero = '100'
        ciudad = 'Montevideo'; referenciaExterna = $Referencia
        # Coordenadas dentro de la zona "Montevideo Centro y Cordón": evita depender del geocodificador
        latitud = $Latitud; longitud = $Longitud
        bultos = @(@{ peso = 2; alto = 10; ancho = 10; profundo = 10 })
    }
}

# 1. Sesión del comercio
$login = Llamar POST '/api/auth/login' @{ email = $Email; password = $Password }
if ($login.Codigo -ne 200) { throw "Login falló ($($login.Codigo))." }
$jwt = @{ Authorization = "Bearer $($login.Json.token)" }
Write-Host "Sesión iniciada como $Email" -ForegroundColor Cyan

# 2. Clave de pruebas
$sb = Llamar GET '/api/comercio/clave-api-pruebas' $null $jwt
if ($sb.Codigo -ne 200) { throw "No se obtuvo la clave de pruebas ($($sb.Codigo))." }
$claveSb = @{ 'X-Api-Key' = $sb.Json.clavePruebas }

$ruta = '/api/integracion/v1/envios'
$ref = "PRUEBA-$([guid]::NewGuid().ToString('N').Substring(0,8))"

# 3. Casos con la clave de pruebas
Verificar 'Sandbox válido' 200 (Llamar POST $ruta (NuevoPedido $ref) $claveSb) { param($j) $j.sandbox -eq $true }
Verificar 'Clave inválida' 401 (Llamar POST $ruta (NuevoPedido $ref) @{ 'X-Api-Key' = 'clave-falsa' })
$malo = NuevoPedido $ref; $malo.bultos = @()
Verificar 'Pedido inválido (sin bultos)' 400 (Llamar POST $ruta $malo $claveSb)

# 4. Clave real (opcional, regenera la clave)
if ($ProbarReal) {
    $real = Llamar POST '/api/comercio/clave-api' $null $jwt
    if ($real.Codigo -ne 200) { throw "No se generó la clave real ($($real.Codigo))." }
    $claveReal = @{ 'X-Api-Key' = $real.Json.claveApi }
    Verificar 'Alta real' 201 (Llamar POST $ruta (NuevoPedido $ref) $claveReal)
    Verificar 'Reintento misma referencia' 200 (Llamar POST $ruta (NuevoPedido $ref) $claveReal) { param($j) $j.duplicado -eq $true }
}

# 5. Límite de tasa de la clave de pruebas (20/min)
$vio429 = $false
foreach ($i in 1..$RafagaLimite) {
    if ((Llamar POST $ruta (NuevoPedido "$ref-$i") $claveSb).Codigo -eq 429) { $vio429 = $true; break }
}
if ($vio429) { Write-Host "[OK]    Límite de tasa -> 429 tras la ráfaga" -ForegroundColor Green }
else { Write-Host "[FALLO] Límite de tasa: no apareció 429 en $RafagaLimite pedidos" -ForegroundColor Red; $fallos++ }

if ($fallos -eq 0) { Write-Host "`nTodo OK" -ForegroundColor Green; exit 0 }
Write-Host "`n$fallos caso(s) fallaron" -ForegroundColor Red; exit 1
