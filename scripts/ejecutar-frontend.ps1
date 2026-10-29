<#
.SYNOPSIS
    Script para ejecutar las aplicaciones de Frontend de forma aislada o conectadas al Backend.
.DESCRIPTION
    Permite iniciar Backoffice, Portal del Comercio y Seguimiento Público utilizando 'dotnet watch'
    para habilitar Hot Reload automático al modificar archivos Razor, HTML y CSS.
    Soporta tanto el modo Mocks en memoria (por defecto) como el modo conectado a la API Backend real (-UsarMocks $false).
#>

param (
    [ValidateSet("todas", "backoffice", "portal", "seguimiento", "menu")]
    [string]$Aplicacion = "menu",

    [bool]$UsarMocks = $true
)

# Establecer la codificación para caracteres en español
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$directorioRaiz = Resolve-Path "$PSScriptRoot\.."
$script:modoMocksActivo = $UsarMocks

function Detener-WatchersProyecto {
    param (
        [string]$rutaProyecto
    )
    $nombreCarpeta = Split-Path $rutaProyecto -Leaf
    $procesosDotnet = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue
    if ($procesosDotnet) {
        foreach ($proc in $procesosDotnet) {
            if ($proc.CommandLine -and ($proc.CommandLine -match [regex]::Escape($nombreCarpeta))) {
                Write-Host "Deteniendo proceso dotnet watch previo ($nombreCarpeta, PID $($proc.ProcessId))..." -ForegroundColor Yellow
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Liberar-Puerto {
    param (
        [string]$puerto,
        [string]$rutaProyecto = ""
    )
    if ($rutaProyecto) {
        Detener-WatchersProyecto -rutaProyecto $rutaProyecto
    }

    $conexiones = Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue
    if ($conexiones) {
        foreach ($conn in $conexiones) {
            $idProceso = $conn.OwningProcess
            $procesoWmi = Get-CimInstance Win32_Process -Filter "ProcessId = $idProceso" -ErrorAction SilentlyContinue
            $proceso = Get-Process -Id $idProceso -ErrorAction SilentlyContinue
            if ($proceso) {
                Write-Host "Liberando puerto $puerto (deteniendo $($proceso.ProcessName) con PID $($proceso.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $proceso.Id -Force -ErrorAction SilentlyContinue
            }
            # Si fue lanzado por un dotnet watch padre, detener también el proceso padre para liberar bloqueos de compilación
            if ($procesoWmi -and $procesoWmi.ParentProcessId) {
                $padre = Get-Process -Id $procesoWmi.ParentProcessId -ErrorAction SilentlyContinue
                if ($padre -and $padre.ProcessName -eq "dotnet") {
                    Stop-Process -Id $padre.Id -Force -ErrorAction SilentlyContinue
                }
            }
        }
        # Espera activa hasta que el socket del sistema operativo se libere por completo
        $intentos = 0
        while ((Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue) -and ($intentos -lt 25)) {
            Start-Sleep -Milliseconds 200
            $intentos++
        }
    }
}

function Esperar-PuertoActivo {
    param (
        [string]$puerto,
        [int]$segundosMaximo = 25
    )
    $intentosMaximos = $segundosMaximo * 2
    $intentoActual = 0
    while ($intentoActual -lt $intentosMaximos) {
        $escuchando = Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue
        if ($escuchando) {
            return $true
        }
        Start-Sleep -Milliseconds 500
        $intentoActual++
    }
    return $false
}

function Sincronizar-ConfiguracionSeguimiento {
    param (
        [bool]$usarMocksActual
    )
    $rutaAppsettingsDev = Join-Path $directorioRaiz "src\Web\SeguimientoPublico\wwwroot\appsettings.Development.json"
    $valorJson = if ($usarMocksActual) { "true" } else { "false" }
    $contenido = "{`n  `"ApiSettings`": {`n    `"BaseUrl`": `"http://localhost:5000`"`n  },`n  `"UsarMocks`": $valorJson`n}"
    Set-Content -Path $rutaAppsettingsDev -Value $contenido -Encoding UTF8
}

function Iniciar-Proyecto {
    param (
        [string]$nombre,
        [string]$rutaProyecto,
        [string]$puerto,
        [bool]$enNuevaVentana = $false,
        [bool]$sinCompilar = $false
    )

    # Verifica si el puerto o un watcher previo sigue activo y lo libera antes de arrancar
    Liberar-Puerto -puerto $puerto -rutaProyecto $rutaProyecto

    $valorMocksTexto = if ($script:modoMocksActivo) { "true" } else { "false" }
    $etiquetaModo = if ($script:modoMocksActivo) { "Modo Mocks en memoria" } else { "Modo API Backend real (http://localhost:5000)" }

    if ($rutaProyecto -match "SeguimientoPublico") {
        Sincronizar-ConfiguracionSeguimiento -usarMocksActual $script:modoMocksActivo
    }

    Write-Host "Iniciando $nombre en http://localhost:$puerto [$etiquetaModo] con Hot Reload..." -ForegroundColor Cyan

    $flagNoBuild = if ($sinCompilar) { "--no-build " } else { "" }
    $argumentosDotnet = "watch --project `"$rutaProyecto`" run $flagNoBuild--launch-profile http -- --UsarMocks=$valorMocksTexto"

    if ($enNuevaVentana) {
        # Abre el proceso en una ventana de terminal independiente configurando UsarMocks según el modo elegido
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:UsarMocks='$valorMocksTexto'; cd `"$directorioRaiz`"; Write-Host '=== $nombre (http://localhost:$puerto) [$etiquetaModo] ===' -ForegroundColor Green; dotnet $argumentosDotnet"
    }
    else {
        $env:UsarMocks = $valorMocksTexto
        Set-Location $directorioRaiz
        if ($sinCompilar) {
            dotnet watch --project $rutaProyecto run --no-build --launch-profile http -- --UsarMocks=$valorMocksTexto
        }
        else {
            dotnet watch --project $rutaProyecto run --launch-profile http -- --UsarMocks=$valorMocksTexto
        }
    }
}

function Iniciar-Todas {
    # 1. Primero libera los puertos y detiene cualquier instancia previa para que no bloqueen bin/ ni obj/
    # durante la generación de manifiestos de Blazor WebAssembly (staticwebassets.endpoints.json).
    Write-Host "Verificando y liberando instancias previas en puertos 5002, 5003 y 5004..." -ForegroundColor Yellow
    Liberar-Puerto -puerto "5002" -rutaProyecto "src/Web/Backoffice"
    Liberar-Puerto -puerto "5003" -rutaProyecto "src/Web/PortalComercio"
    Liberar-Puerto -puerto "5004" -rutaProyecto "src/Web/SeguimientoPublico"

    Sincronizar-ConfiguracionSeguimiento -usarMocksActual $script:modoMocksActivo

    # 2. Compila una sola vez con los binarios desbloqueados para evitar colisiones entre procesos
    Write-Host "Compilando el frontend una sola vez antes de lanzar las aplicaciones..." -ForegroundColor Cyan
    Set-Location $directorioRaiz
    dotnet build UltimaMilla.Frontend.slnf
    if ($LASTEXITCODE -ne 0) {
        Write-Host "La compilación falló. No se lanzan las aplicaciones." -ForegroundColor Red
        return
    }

    # 3. Inicia secuencialmente esperando que cada servidor levante antes de lanzar el siguiente
    Iniciar-Proyecto -nombre "Backoffice del Operador" -rutaProyecto "src/Web/Backoffice" -puerto "5002" -enNuevaVentana $true
    $null = Esperar-PuertoActivo -puerto "5002" -segundosMaximo 20

    Iniciar-Proyecto -nombre "Portal del Comercio" -rutaProyecto "src/Web/PortalComercio" -puerto "5003" -enNuevaVentana $true
    $null = Esperar-PuertoActivo -puerto "5003" -segundosMaximo 25

    Iniciar-Proyecto -nombre "Seguimiento Público" -rutaProyecto "src/Web/SeguimientoPublico" -puerto "5004" -enNuevaVentana $true
    $null = Esperar-PuertoActivo -puerto "5004" -segundosMaximo 20
}

function Mostrar-Menu {
    Clear-Host
    $descripcionModo = if ($script:modoMocksActivo) {
        "Mocks en memoria (Sin dependencias de backend/Docker)"
    }
    else {
        "API Backend real (Conectado a http://localhost:5000)"
    }

    Write-Host "==========================================================" -ForegroundColor Blue
    Write-Host "   PLATAFORMA ÚLTIMA MILLA - ENTORNO FRONTEND (.NET 10)   " -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Blue
    Write-Host "Modo actual: $descripcionModo" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Seleccione qué aplicación desea ejecutar:"
    Write-Host " [1] Todas las aplicaciones en simultáneo (ventanas separadas con Hot Reload)"
    Write-Host " [2] Backoffice del Operador (Razor Pages - http://localhost:5002)"
    Write-Host " [3] Portal del Comercio (Blazor WASM - http://localhost:5003)"
    Write-Host " [4] Seguimiento Público (LittleBox Envíos - http://localhost:5004)"
    Write-Host " [5] Compilar proyectos de Frontend (verificar compilación sin ejecutar)"
    Write-Host " [6] Detener procesos y liberar puertos (5002, 5003 y 5004)"
    Write-Host " [7] Alternar modo de datos (Mocks en memoria <-> API Backend real)"
    Write-Host " [0] Salir"
    Write-Host ""

    $opcion = Read-Host "Ingrese una opción [1-7 o 0]"

    switch ($opcion) {
        "1" {
            Write-Host "`nLanzando las 3 aplicaciones en ventanas separadas..." -ForegroundColor Green
            Iniciar-Todas
            Write-Host "`nLas 3 aplicaciones están corriendo. Puede editar archivos .razor, .cshtml o .css para ver los cambios en vivo." -ForegroundColor Green
        }
        "2" {
            Iniciar-Proyecto -nombre "Backoffice del Operador" -rutaProyecto "src/Web/Backoffice" -puerto "5002" -enNuevaVentana $false
        }
        "3" {
            Iniciar-Proyecto -nombre "Portal del Comercio" -rutaProyecto "src/Web/PortalComercio" -puerto "5003" -enNuevaVentana $false
        }
        "4" {
            Iniciar-Proyecto -nombre "Seguimiento Público" -rutaProyecto "src/Web/SeguimientoPublico" -puerto "5004" -enNuevaVentana $false
        }
        "5" {
            Write-Host "`nLiberando bloqueos y compilando filtro de solución Frontend..." -ForegroundColor Cyan
            Liberar-Puerto -puerto "5002" -rutaProyecto "src/Web/Backoffice"
            Liberar-Puerto -puerto "5003" -rutaProyecto "src/Web/PortalComercio"
            Liberar-Puerto -puerto "5004" -rutaProyecto "src/Web/SeguimientoPublico"
            Set-Location $directorioRaiz
            dotnet build UltimaMilla.Frontend.slnf
            Write-Host "`nPresione cualquier tecla para volver al menú..."
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            Mostrar-Menu
        }
        "6" {
            Write-Host "`nLiberando puertos 5002, 5003 y 5004..." -ForegroundColor Yellow
            Liberar-Puerto -puerto "5002" -rutaProyecto "src/Web/Backoffice"
            Liberar-Puerto -puerto "5003" -rutaProyecto "src/Web/PortalComercio"
            Liberar-Puerto -puerto "5004" -rutaProyecto "src/Web/SeguimientoPublico"
            Write-Host "Puertos liberados exitosamente." -ForegroundColor Green
            Start-Sleep -Seconds 1
            Mostrar-Menu
        }
        "7" {
            $script:modoMocksActivo = -not $script:modoMocksActivo
            Mostrar-Menu
        }
        "0" {
            Write-Host "Finalizado." -ForegroundColor Gray
            exit
        }
        default {
            Write-Host "Opción inválida. Intente de nuevo." -ForegroundColor Red
            Start-Sleep -Seconds 1
            Mostrar-Menu
        }
    }
}

# Ejecución según parámetro o menú interactivo
switch ($Aplicacion) {
    "todas" {
        Iniciar-Todas
    }
    "backoffice" {
        Iniciar-Proyecto -nombre "Backoffice del Operador" -rutaProyecto "src/Web/Backoffice" -puerto "5002" -enNuevaVentana $false
    }
    "portal" {
        Iniciar-Proyecto -nombre "Portal del Comercio" -rutaProyecto "src/Web/PortalComercio" -puerto "5003" -enNuevaVentana $false
    }
    "seguimiento" {
        Iniciar-Proyecto -nombre "Seguimiento Público" -rutaProyecto "src/Web/SeguimientoPublico" -puerto "5004" -enNuevaVentana $false
    }
    default {
        Mostrar-Menu
    }
}
