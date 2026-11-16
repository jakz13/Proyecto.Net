@echo off
REM Despliega el proyecto en la VM de Azure. Ejemplos:
REM   scripts\desplegar.bat              (api, worker, backoffice, portal, seguimiento y panel)
REM   scripts\desplegar.bat api          (solo la API)
REM   scripts\desplegar.bat api portal
REM   scripts\desplegar.bat mobile       (publica el APK firmado para la actualizacion OTA)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desplegar.ps1" %*
echo.
pause
