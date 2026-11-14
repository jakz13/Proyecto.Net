<#
.SYNOPSIS
    Simula a un repartidor real recorriendo su hoja de ruta, contra la API de verdad.

.DESCRIPTION
    Hace lo mismo que la app móvil, pero con la ubicación inventada: inicia sesión como repartidor, toma su hoja de ruta activa
    y la recorre parada por parada siguiendo calles reales (OSRM). En cada tramo envía posiciones GPS (POST /api/mobile/v1/posiciones),
    marca "en camino" y "llegada" en cada parada y registra la entrega o el intento fallido. Todo queda en la base y se ve en vivo
    en el Backoffice (Tablero en Vivo, detalle de la hoja de ruta, Envíos) y en el seguimiento público.

    Con las credenciales de un despachador además abre y cierra incidencias reales (tráfico, avería) en la hoja de ruta.

    Requisitos: una hoja de ruta ya liberada (o en carga / despachada) asignada a ese repartidor. Si está liberada, el script confirma la
    carga con los bultos de la ruta y la despacha, como haría el repartidor al cargar el vehículo.

    Las contraseñas no van en el repo: pasarlas por parámetro, por las variables de entorno ULTIMAMILLA_PASS_REPARTIDOR /
    ULTIMAMILLA_PASS_DESPACHADOR, o escribirlas cuando el script las pida.

.PARAMETER Api
    URL base de la API. Servidor: https://shippingbox.duckdns.org  |  Local: la URL con que corre UltimaMilla.Api.

.PARAMETER Escenario
    Demo   -> tráfico entre la parada 1 y 2, cliente ausente en la parada 3, avería entre la 3 y la 4; el resto se entrega.
    Limpio -> sin incidencias: todas las paradas se entregan.

.PARAMETER Factor
    Cuántas veces más rápido que la realidad. A 32 km/h reales, x8 recorre una ciudad en pocos minutos. Con 1 va en tiempo real.

.EXAMPLE
    .\scripts\simular-repartidor.ps1 -EmailRepartidor repartidor@gmail.com -EmailDespachador despacho@gmail.com

.EXAMPLE
    .\scripts\simular-repartidor.ps1 -Api http://localhost:5000 -EmailRepartidor repartidor@gmail.com -Escenario Limpio -Factor 15
#>
param(
    [string]$Api = 'https://shippingbox.duckdns.org',
    [Parameter(Mandatory = $true)][string]$EmailRepartidor,
    [string]$PasswordRepartidor = $env:ULTIMAMILLA_PASS_REPARTIDOR,
    [string]$EmailDespachador,
    [string]$PasswordDespachador = $env:ULTIMAMILLA_PASS_DESPACHADOR,
    [string]$RutaId,
    [ValidateSet('Demo', 'Limpio')][string]$Escenario = 'Demo',
    [double]$Factor = 8,
    [double]$VelocidadKmh = 32,
    [int]$IntervaloSeg = 2,
    [double]$SalidaLat = -34.8836,   # Depósito Logístico Central (Gral. Flores 2450)
    [double]$SalidaLng = -56.1865,
    [switch]$SinOsrm
)

$ErrorActionPreference = 'Stop'
$Api = $Api.TrimEnd('/')
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function Log($texto, $color = 'Gray') { Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $texto) -ForegroundColor $color }

function Leer-Password($valor, $quien) {
    if ($valor) { return $valor }
    $seguro = Read-Host "Contraseña de $quien" -AsSecureString
    return [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro))
}

function Llamar($metodo, $ruta, $cuerpo, $token) {
    $parametros = @{ Method = $metodo; Uri = "$Api$ruta"; ContentType = 'application/json; charset=utf-8'; TimeoutSec = 30 }
    if ($token) { $parametros.Headers = @{ Authorization = "Bearer $token" } }
    # En UTF-8 explícito: Windows PowerShell 5.1 enviaría Latin-1 y la API rechazaría los acentos
    if ($null -ne $cuerpo) { $parametros.Body = [System.Text.Encoding]::UTF8.GetBytes(($cuerpo | ConvertTo-Json -Depth 8 -Compress)) }
    try {
        return Invoke-RestMethod @parametros
    } catch {
        $detalle = ''
        if ($_.Exception.Response) {
            try { $detalle = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { }
        }
        throw "$metodo $ruta falló: $($_.Exception.Message) $detalle"
    }
}

function Iniciar-Sesion($email, $password) {
    $r = Llamar 'POST' '/api/v1/auth/login' @{ email = $email; password = $password } $null
    return $r.token
}

# ------------------------------------------------------------------ geometría

function Distancia-M($a, $b) {
    $rad = [Math]::PI / 180
    $dLat = ($b[0] - $a[0]) * $rad
    $dLng = ($b[1] - $a[1]) * $rad
    $x = [Math]::Sin($dLat / 2) * [Math]::Sin($dLat / 2) + [Math]::Cos($a[0] * $rad) * [Math]::Cos($b[0] * $rad) * [Math]::Sin($dLng / 2) * [Math]::Sin($dLng / 2)
    return 2 * 6371000 * [Math]::Asin([Math]::Sqrt($x))
}

# Camino por calles entre dos puntos; si OSRM no responde, línea recta con puntos intermedios
function Obtener-Tramo($desde, $hasta) {
    if (-not $SinOsrm) {
        try {
            $url = 'https://router.project-osrm.org/route/v1/driving/{0},{1};{2},{3}?overview=full&geometries=geojson' -f `
                $desde[1].ToString($inv), $desde[0].ToString($inv), $hasta[1].ToString($inv), $hasta[0].ToString($inv)
            $r = Invoke-RestMethod -Uri $url -TimeoutSec 10
            if ($r.code -eq 'Ok' -and $r.routes.Count -gt 0) {
                $pts = @()
                foreach ($c in $r.routes[0].geometry.coordinates) { $pts += , @([double]$c[1], [double]$c[0]) }
                if ($pts.Count -gt 1) { return $pts }
            }
        } catch { Log 'OSRM no respondió: se usa línea recta para este tramo.' 'DarkYellow' }
    }
    $pts = @()
    for ($i = 0; $i -le 30; $i++) {
        $t = $i / 30.0
        $pts += , @(($desde[0] + ($hasta[0] - $desde[0]) * $t), ($desde[1] + ($hasta[1] - $desde[1]) * $t))
    }
    return $pts
}

# ------------------------------------------------------------------ envío de posición

$script:token = $null
$script:rutaId = $null
$script:bateria = 92
$script:ultima = @($SalidaLat, $SalidaLng)

function Enviar-Posicion($punto, $kmh) {
    $script:ultima = $punto
    $script:bateria = [Math]::Max(15, $script:bateria - 0.03)
    $cuerpo = @{ posiciones = @(@{
        latitud = [Math]::Round($punto[0], 6); longitud = [Math]::Round($punto[1], 6)
        tomadaEn = (Get-Date).ToUniversalTime().ToString('o')
        velocidadKmh = [Math]::Round($kmh, 1); bateriaPct = [int]$script:bateria
        hojaDeRutaId = $script:rutaId
    }) }
    Llamar 'POST' '/api/mobile/v1/posiciones' $cuerpo $script:token | Out-Null
}

# Recorre la geometría enviando una posición cada $IntervaloSeg. $mult baja la velocidad (tráfico).
function Recorrer($puntos, $mult) {
    $kmh = $VelocidadKmh * $mult
    $pasoM = ($kmh / 3.6) * $IntervaloSeg * $Factor
    $faltante = $pasoM   # metros que faltan para el próximo envío de posición
    for ($i = 1; $i -lt $puntos.Count; $i++) {
        $seg = Distancia-M $puntos[$i - 1] $puntos[$i]
        if ($seg -le 0) { continue }
        $andado = 0.0
        while ($andado + $faltante -le $seg) {
            $andado += $faltante
            $faltante = $pasoM
            $t = $andado / $seg
            $p = @(($puntos[$i - 1][0] + ($puntos[$i][0] - $puntos[$i - 1][0]) * $t), ($puntos[$i - 1][1] + ($puntos[$i][1] - $puntos[$i - 1][1]) * $t))
            $v = $kmh * (1 + 0.1 * [Math]::Sin([Environment]::TickCount / 3000.0))
            Enviar-Posicion $p $v
            Start-Sleep -Seconds $IntervaloSeg
        }
        $faltante -= ($seg - $andado)
    }
    Enviar-Posicion $puntos[$puntos.Count - 1] $kmh
}

# Vehículo detenido (avería, atención en la puerta): sigue informando la misma posición con velocidad 0
function Permanecer($segundos) {
    $fin = (Get-Date).AddSeconds($segundos)
    while ((Get-Date) -lt $fin) {
        Enviar-Posicion $script:ultima 0
        Start-Sleep -Seconds $IntervaloSeg
    }
}

# ------------------------------------------------------------------ incidencias reales (despachador)

$script:tokenDespachador = $null

function Abrir-Incidencia($tipo, $descripcion, $severidad) {
    if (-not $script:tokenDespachador) { Log "Incidencia '$tipo' simulada (sin credenciales de despachador no se registra en el sistema)." 'DarkYellow'; return $null }
    $r = Llamar 'POST' '/api/backoffice/incidencias' @{ tipo = $tipo; descripcion = $descripcion; severidad = $severidad; hojaDeRutaId = $script:rutaId } $script:tokenDespachador
    Log "Incidencia abierta: $tipo ($severidad)." 'Yellow'
    return $r.id
}

function Cerrar-Incidencia($id, $resolucion) {
    if (-not $id -or -not $script:tokenDespachador) { return }
    Llamar 'POST' "/api/backoffice/incidencias/$id/cierre" @{ resolucion = $resolucion } $script:tokenDespachador | Out-Null
    Log "Incidencia cerrada: $resolucion" 'Green'
}

# ------------------------------------------------------------------ programa

Log "Iniciando sesión como repartidor ($EmailRepartidor)..." 'Cyan'
$script:token = Iniciar-Sesion $EmailRepartidor (Leer-Password $PasswordRepartidor 'repartidor')

if ($EmailDespachador) {
    Log "Iniciando sesión como despachador ($EmailDespachador) para registrar incidencias..." 'Cyan'
    $script:tokenDespachador = Iniciar-Sesion $EmailDespachador (Leer-Password $PasswordDespachador 'despachador')
} elseif ($Escenario -eq 'Demo') {
    Log 'Sin -EmailDespachador: las incidencias de tráfico y avería afectan el movimiento pero no se registran en el módulo de Incidencias.' 'DarkYellow'
}

try {
    $rutas = @(Llamar 'GET' '/api/mobile/v1/rutas/activas' $null $script:token)
} catch {
    # Servidores sin el listado: se usa la ruta activa única (404 = no tiene ninguna)
    try { $rutas = @(Llamar 'GET' '/api/mobile/v1/rutas/activa' $null $script:token) } catch { $rutas = @() }
}
if ($RutaId) { $rutas = @($rutas | Where-Object { $_.hojaDeRutaId -eq $RutaId }) }
$ruta = $rutas | Where-Object { $_.estado -in @('Despachada', 'EnTransito', 'EnCarga', 'Liberada') } | Select-Object -First 1
if (-not $ruta) { throw 'El repartidor no tiene una hoja de ruta vigente (Liberada, EnCarga, Despachada o EnTransito). Libérala desde el Backoffice y vuelve a correr el script.' }

$script:rutaId = $ruta.hojaDeRutaId
$paradas = @($ruta.paradas | Where-Object { $_.estadoParada -in @('Pendiente', 'EnCamino', 'EnSitio') } | Sort-Object orden)
Log ("Hoja de ruta {0} ({1}), vehículo {2}: {3} paradas pendientes." -f $ruta.numeroRuta, $ruta.estado, $ruta.patenteVehiculo, $paradas.Count) 'Cyan'
if ($paradas.Count -eq 0) { throw 'La hoja de ruta no tiene paradas pendientes.' }

# Carga y despacho, como lo hace el repartidor en el depósito
$estadoRuta = $ruta.estado

# 1) Liberada -> EnCarga: el repartidor escanea y confirma la carga desde la app
if ($estadoRuta -eq 'Liberada') {
    $codigos = @($ruta.paradas | ForEach-Object { $_.resumenBultos.codigosBultos } | Where-Object { $_ })
    Log "Confirmando la carga ($($codigos.Count) bultos)..." 'Cyan'
    Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/carga/confirmar" @{ codigosBultosCargados = $codigos; idempotencyKey = "sim-carga-$([guid]::NewGuid())" } $script:token | Out-Null
    $estadoRuta = 'EnCarga'
}

# 2) EnCarga -> Despachada: el despacho de salida lo hace el despachador u operario (los envíos pasan a En tránsito)
if ($estadoRuta -eq 'EnCarga') {
    if (-not $script:tokenDespachador) { throw 'La carga está confirmada pero falta el despacho de salida: pásale -EmailDespachador (o despáchala desde el Backoffice) y vuelve a correr el script.' }
    Log 'Despacho de salida (despachador)...' 'Cyan'
    try {
        Llamar 'POST' "/api/planificacion/hojas-de-ruta/$($script:rutaId)/despacho" @{ observaciones = 'Despacho de salida (simulación)' } $script:tokenDespachador | Out-Null
    } catch {
        if ("$_" -match 'sin rendir') { throw "$_`nEl repartidor tiene otra hoja de ruta terminada sin rendir: el operario de depósito debe rendirla (Depósito > Rendición) y luego se vuelve a correr el script." }
        throw
    }
    $estadoRuta = 'Despachada'
}

# 3) Despachada -> EnTransito: el repartidor inicia el recorrido
if ($estadoRuta -eq 'Despachada') {
    Log 'Iniciando recorrido...' 'Cyan'
    Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/iniciar-recorrido" @{ idempotencyKey = "sim-inicio-$([guid]::NewGuid())" } $script:token | Out-Null
}

Log ("Velocidad {0} km/h con factor x{1}: se envía una posición cada {2} s." -f $VelocidadKmh, $Factor, $IntervaloSeg) 'Cyan'
Log 'Abre el Backoffice (Tablero en Vivo o el detalle de la hoja de ruta) para ver el movimiento. Ctrl+C para cortar.' 'Cyan'
Enviar-Posicion $script:ultima 0
Start-Sleep -Seconds $IntervaloSeg

$total = $paradas.Count
$entregadas = 0; $fallidas = 0
for ($i = 0; $i -lt $total; $i++) {
    $p = $paradas[$i]
    $n = $i + 1

    if ($null -eq $p.latitud -or $null -eq $p.longitud) {
        Log "Parada $n ($($p.codigoSeguimiento)) sin coordenadas: se usa un punto cercano al anterior." 'DarkYellow'
        $destino = @(($script:ultima[0] + 0.004), ($script:ultima[1] + 0.004))
    } else {
        $destino = @([double]$p.latitud, [double]$p.longitud)
    }

    Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/paradas/$($p.paradaId)/seleccionar" @{ latitudActual = $script:ultima[0]; longitudActual = $script:ultima[1]; timestamp = (Get-Date).ToUniversalTime().ToString('o'); accion = 'IniciarNavegacion' } $script:token | Out-Null
    Log "Hacia la parada ${n}/${total}: $($p.codigoSeguimiento) - $($p.direccionFormateada)" 'White'

    $incidenciaTramo = $null
    $multiplicador = 1.0
    if ($Escenario -eq 'Demo') {
        if ($n -eq 2) {
            $multiplicador = 0.3
            Log 'Tráfico intenso en el tramo: la velocidad cae.' 'Yellow'
            $incidenciaTramo = Abrir-Incidencia 'Tráfico intenso' 'Congestión en el recorrido hacia la parada 2; el repartidor avanza lento.' 'Media'
        }
        if ($n -eq 4) {
            Log 'Avería del vehículo en el tramo.' 'Red'
            $incidenciaTramo = Abrir-Incidencia 'Avería de vehículo' 'El vehículo se detuvo en la vía; se solicita auxilio mecánico.' 'Alta'
        }
    }

    $tramo = Obtener-Tramo $script:ultima $destino
    if ($Escenario -eq 'Demo' -and $n -eq 4) {
        # A mitad del tramo se detiene
        $mitad = [int]($tramo.Count / 2)
        Recorrer $tramo[0..$mitad] $multiplicador
        Log 'Vehículo detenido 40 s por la avería...' 'Red'
        Permanecer 40
        Cerrar-Incidencia $incidenciaTramo 'Auxilio mecánico resolvió la avería; el vehículo retomó el recorrido.'
        $incidenciaTramo = $null
        Recorrer $tramo[$mitad..($tramo.Count - 1)] $multiplicador
    } else {
        Recorrer $tramo $multiplicador
        Cerrar-Incidencia $incidenciaTramo 'El tráfico se normalizó.'
    }

    Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/paradas/$($p.paradaId)/arribo" @{ latitudActual = $destino[0]; longitudActual = $destino[1]; timestamp = (Get-Date).ToUniversalTime().ToString('o') } $script:token | Out-Null
    Log "Llegó a la parada $n." 'White'

    if ($p.tipo -eq 'Retiro') {
        Log "La parada $n es un retiro: el script no registra retiros, sigue de largo." 'DarkYellow'
        continue
    }

    Permanecer 6

    $falla = ($Escenario -eq 'Demo' -and $n -eq 3)
    if ($falla) {
        $motivo = $ruta.motivosNoEntrega | Where-Object { $_.codigo -match 'AUSENT' -or $_.descripcion -match 'ausente' } | Select-Object -First 1
        if (-not $motivo) { $motivo = $ruta.motivosNoEntrega | Select-Object -First 1 }
        if (-not $motivo) { throw 'El operador no tiene motivos de no entrega configurados.' }
        $r = Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/paradas/$($p.paradaId)/intento-fallido" @{
            envioId = $p.envioId; idempotencyKey = "sim-fallo-$([guid]::NewGuid())"; motivoNoEntregaId = $motivo.id; motivoCodigo = $motivo.codigo
            observaciones = 'Simulación: nadie atendió en el domicilio.'; latitud = $destino[0]; longitud = $destino[1]
            fechaHoraDispositivoUtc = (Get-Date).ToUniversalTime().ToString('o')
        } $script:token
        $fallidas++
        Log "Parada $n NO entregada ($($motivo.descripcion))." 'Red'
    } else {
        # Imagen de 1x1 píxel como firma de prueba: algunas reglas del operador exigen firma
        $firma = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='
        Llamar 'POST' "/api/mobile/v1/rutas/$($script:rutaId)/paradas/$($p.paradaId)/entrega-exitosa" @{
            envioId = $p.envioId; idempotencyKey = "sim-entrega-$([guid]::NewGuid())"
            nombreReceptor = $(if ($p.destinatario.nombre) { $p.destinatario.nombre } else { 'Receptor de prueba' }); documentoReceptor = '12345678'
            firmaDigitalBase64 = $firma; latitud = $destino[0]; longitud = $destino[1]
            fechaHoraDispositivoUtc = (Get-Date).ToUniversalTime().ToString('o')
            codigosBultosEntregados = @($p.resumenBultos.codigosBultos)
        } $script:token | Out-Null
        $entregadas++
        Log "Parada $n entregada." 'Green'
    }
}

Log ("Recorrido terminado: {0} entregadas, {1} no entregadas." -f $entregadas, $fallidas) 'Green'
Log 'La hoja de ruta queda en tránsito; la rendición de vuelta al depósito se hace como siempre desde el Backoffice o la app.' 'Gray'
