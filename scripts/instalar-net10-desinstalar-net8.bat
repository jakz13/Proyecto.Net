@echo off
echo ===================================================
echo Desinstalando .NET 8 e Instalando .NET 10 en Windows
echo ===================================================
echo.
echo 1. Desinstalando .NET 8 SDK...
winget uninstall --id Microsoft.DotNet.SDK.8 --exact --silent
echo.
echo 2. Instalando .NET 10 SDK...
winget install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-package-agreements --accept-source-agreements
echo.
echo ===================================================
echo Proceso finalizado. Verificando version instalada:
dotnet --version
echo ===================================================
pause
