@echo off
setlocal
set "ROOT=%~dp0"

if "%~1"=="" (
  echo Usage: setup-stable-tunnel.bat winremote.example.com
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\setup-stable-cloudflare-tunnel.ps1" -Hostname "%~1"
