@echo off
chcp 65001 > nul
title Ejecutar Frontend - Plataforma Última Milla

echo ==========================================================
echo    PLATAFORMA ÚLTIMA MILLA - ENTORNO FRONTEND (.NET 10)
echo ==========================================================
echo.
echo Iniciando script interactivo de PowerShell...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ejecutar-frontend.ps1"

pause
