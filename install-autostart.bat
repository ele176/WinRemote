@echo off
setlocal
set "ROOT=%~dp0"

call "%ROOT%build.bat"
if errorlevel 1 exit /b %errorlevel%

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\install-agent-autostart.ps1" %*
