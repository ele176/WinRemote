param(
  [int]$Port = 17373,
  [int]$Fps = 8,
  [int]$Width = 1280,
  [int]$Quality = 55
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$exe = Join-Path $root "bin\WinRemoteAgent.exe"

if (-not (Test-Path -LiteralPath $exe)) {
  throw "Missing $exe. Run build.bat first."
}

$taskName = "WinRemoteAgent"
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$arguments = "--bind 127.0.0.1 --port $Port --fps $Fps --width $Width --quality $Quality"

$action = New-ScheduledTaskAction -Execute $exe -Argument $arguments -WorkingDirectory $root
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel LeastPrivilege
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 365)

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Start WinRemote Agent when the current Windows user logs in." -Force | Out-Null

Write-Host "Installed scheduled task: $taskName"
Write-Host "It starts after $currentUser logs in. Windows lock screen and UAC secure desktop are still protected by Windows."
