@echo off
REM Despliega el proyecto en la VM de Azure. Ejemplos:
REM   scripts\desplegar.bat              (api, worker, backoffice y portal)
REM   scripts\desplegar.bat api          (solo la API)
REM   scripts\desplegar.bat api portal
REM   scripts\desplegar.bat mobile       (publica el APK para la actualizacion OTA; solo si se pide)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desplegar.ps1" %*
echo.
pause
