@echo off
setlocal
set "ROOT=%~dp0"
set "CLOUDFLARED=%ROOT%tools\cloudflared.exe"

for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\find-free-port.ps1" -Preferred 17373 -Max 17473`) do set "PORT=%%P"
if "%PORT%"=="" (
  echo Could not find a free local port.
  exit /b 1
)

call "%ROOT%build.bat"
if errorlevel 1 exit /b %errorlevel%

if not exist "%CLOUDFLARED%" (
  echo cloudflared.exe was not found.
  echo Downloading Cloudflare Tunnel into "%ROOT%tools"...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\download-cloudflared.ps1"
  if errorlevel 1 exit /b %errorlevel%
)

echo.
echo Starting WinRemote Agent on localhost:%PORT%.
echo Keep the Agent window open. It shows the 8-digit pairing code.
start "WinRemote Agent" "%ROOT%bin\WinRemoteAgent.exe" --bind 127.0.0.1 --port %PORT%

timeout /t 2 /nobreak > nul

echo.
echo Starting public HTTPS tunnel.
echo Copy the https://*.trycloudflare.com URL into Safari on your iPhone.
echo If it does not open immediately, wait until you see: Registered tunnel connection
echo.
"%CLOUDFLARED%" tunnel --url http://127.0.0.1:%PORT% --protocol http2
