<#
.SYNOPSIS
    Script para ejecutar las aplicaciones de Frontend de forma aislada para desarrollo visual.
.DESCRIPTION
    Permite iniciar Backoffice, Portal del Comercio y Seguimiento Público utilizando 'dotnet watch'
    para habilitar Hot Reload automático al modificar archivos Razor, HTML y CSS.
    No requiere base de datos ni Docker, ya que utiliza la capa de mocks en memoria.
#>

param (
    [ValidateSet("todas", "backoffice", "portal", "seguimiento", "menu")]
    [string]$Aplicacion = "menu"
)

# Establecer la codificación para caracteres en español
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$directorioRaiz = Resolve-Path "$PSScriptRoot\.."

function Liberar-Puerto {
    param (
        [string]$puerto
    )
    $conexiones = Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue
    if ($conexiones) {
        foreach ($conn in $conexiones) {
            $proceso = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proceso) {
                Write-Host "Liberando puerto $puerto (deteniendo $($proceso.ProcessName) con PID $($proceso.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $proceso.Id -Force -ErrorAction SilentlyContinue
            }
        }
        # Espera activa hasta que el socket del sistema operativo se libere por completo
        $intentos = 0
        while ((Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue) -and ($intentos -lt 15)) {
            Start-Sleep -Milliseconds 200
            $intentos++
        }
    }
}

function Iniciar-Proyecto {
    param (
        [string]$nombre,
        [string]$rutaProyecto,
        [string]$puerto,
        [bool]$enNuevaVentana = $false
    )

    # Verifica si el puerto ya está en uso y lo libera para evitar error de socket ocupado
    Liberar-Puerto -puerto $puerto

    Write-Host "Iniciando $nombre en http://localhost:$puerto [Modo Mocks en memoria] con Hot Reload..." -ForegroundColor Cyan

    $argumentosDotnet = "watch --project `"$rutaProyecto`" run --launch-profile http -- --UsarMocks=true"

    if ($enNuevaVentana) {
        # Abre el proceso en una ventana de terminal independiente configurando UsarMocks=true
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:UsarMocks='true'; cd `"$directorioRaiz`"; Write-Host '=== $nombre (http://localhost:$puerto) [Modo Mocks en Memoria] ===' -ForegroundColor Green; dotnet $argumentosDotnet"
    }
    else {
        $env:UsarMocks = 'true'
        Set-Location $directorioRaiz
        dotnet watch --project $rutaProyecto run --launch-profile http -- --UsarMocks=true
    }
}

function Iniciar-Todas {
    # Compila una sola vez para evitar que los 3 'dotnet watch' compilen en paralelo
    # el proyecto compartido Frontend.Contracts (error CS2012: archivo bloqueado).
    Write-Host "Compilando el frontend una sola vez antes de lanzar las aplicaciones..." -ForegroundColor Cyan
    Set-Location $directorioRaiz
    dotnet build UltimaMilla.Frontend.slnf
    if ($LASTEXITCODE -ne 0) {
        Write-Host "La compilación falló. No se lanzan las aplicaciones." -ForegroundColor Red
        return
    }

    Iniciar-Proyecto -nombre "Backoffice del Operador" -rutaProyecto "src/Web/Backoffice" -puerto "5002" -enNuevaVentana $true
    Start-Sleep -Seconds 5
    Iniciar-Proyecto -nombre "Portal del Comercio" -rutaProyecto "src/Web/PortalComercio" -puerto "5003" -enNuevaVentana $true
    Start-Sleep -Seconds 5
    Iniciar-Proyecto -nombre "Seguimiento Público" -rutaProyecto "src/Web/SeguimientoPublico" -puerto "5004" -enNuevaVentana $true
}

function Mostrar-Menu {
    Clear-Host
    Write-Host "==========================================================" -ForegroundColor Blue
    Write-Host "   PLATAFORMA ÚLTIMA MILLA - ENTORNO FRONTEND (.NET 10)   " -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Blue
    Write-Host "Modo: Mocks en memoria (Sin dependencias de backend/Docker)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Seleccione qué aplicación desea ejecutar para diseñar y hacer cambios visuales:"
    Write-Host " [1] Todas las aplicaciones en simultáneo (ventanas separadas con Hot Reload)"
    Write-Host " [2] Backoffice del Operador (Razor Pages - http://localhost:5002)"
    Write-Host " [3] Portal del Comercio (Blazor WASM - http://localhost:5003)"
    Write-Host " [4] Seguimiento Público (LittleBox Envíos - http://localhost:5004)"
    Write-Host " [5] Compilar proyectos de Frontend (verificar compilación sin ejecutar)"
    Write-Host " [6] Detener procesos y liberar puertos (5002, 5003 y 5004)"
    Write-Host " [0] Salir"
    Write-Host ""

    $opcion = Read-Host "Ingrese una opción [1-5 o 0]"

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
            Write-Host "`nCompilando filtro de solución Frontend..." -ForegroundColor Cyan
            Set-Location $directorioRaiz
            dotnet build UltimaMilla.Frontend.slnf
            Write-Host "`nPresione cualquier tecla para volver al menú..."
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            Mostrar-Menu
        }
        "6" {
            Write-Host "`nLiberando puertos 5002, 5003 y 5004..." -ForegroundColor Yellow
            Liberar-Puerto -puerto "5002"
            Liberar-Puerto -puerto "5003"
            Liberar-Puerto -puerto "5004"
            Write-Host "Puertos liberados exitosamente." -ForegroundColor Green
            Start-Sleep -Seconds 1
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
