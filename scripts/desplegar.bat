@echo off
REM Despliega el proyecto en la VM de Oracle Cloud. Ejemplos:
REM   scripts\desplegar.bat              (api, worker y backoffice)
REM   scripts\desplegar.bat api          (solo la API)
REM   scripts\desplegar.bat api backoffice
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desplegar.ps1" %*
echo.
pause
