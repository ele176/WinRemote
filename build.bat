@echo off
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
  echo Could not find csc.exe from .NET Framework 4.x.
  exit /b 1
)

if not exist "%ROOT%bin" mkdir "%ROOT%bin"

"%CSC%" /nologo /target:exe /out:"%ROOT%bin\WinRemoteAgent.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "%ROOT%src\WinRemoteAgent.cs"
exit /b %errorlevel%
