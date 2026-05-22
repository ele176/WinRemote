@echo off
setlocal
set "ROOT=%~dp0"

call "%ROOT%build.bat"
if errorlevel 1 exit /b %errorlevel%

"%ROOT%bin\WinRemoteAgent.exe" %*
