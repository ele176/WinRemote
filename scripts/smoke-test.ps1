param(
  [int]$PreferredPort = 17373
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root

try {
  & .\build.bat
  if ($LASTEXITCODE -ne 0) {
    throw "build.bat failed with exit code $LASTEXITCODE"
  }

  $port = & .\scripts\find-free-port.ps1 -Preferred $PreferredPort -Max ($PreferredPort + 100)
  $exe = Join-Path $root "bin\WinRemoteAgent.exe"
  $token = "smoke-token"
  $pairCode = "12345678"

  $psi = [System.Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = $exe
  $psi.Arguments = "--bind 127.0.0.1 --port $port --token $token --pair-code $pairCode"
  $psi.WorkingDirectory = $root
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true

  $process = [System.Diagnostics.Process]::new()
  $process.StartInfo = $psi
  [void]$process.Start()

  try {
    $baseUrl = "http://127.0.0.1:$port"
    $ready = $false

    for ($i = 0; $i -lt 30; $i++) {
      Start-Sleep -Milliseconds 250
      try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/" -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
          $ready = $true
          break
        }
      }
      catch {
      }
    }

    if (-not $ready) {
      throw "Agent did not become ready on port $port"
    }

    $homeResponse = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/" -TimeoutSec 5
    if ($homeResponse.StatusCode -ne 200 -or $homeResponse.Content -notmatch "WinRemote") {
      throw "Home page smoke check failed"
    }

    $status = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/api/status" -Headers @{ "X-Remote-Token" = $token } -TimeoutSec 5
    if ($status.StatusCode -ne 200 -or $status.Content -notmatch '"ok":true') {
      throw "Status API smoke check failed"
    }

    $pair = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/api/pair" -Method POST -Body "code=$pairCode" -ContentType "application/x-www-form-urlencoded" -TimeoutSec 5
    if ($pair.StatusCode -ne 200 -or $pair.Content -notmatch $token) {
      throw "Pair API smoke check failed"
    }

    Write-Host "Smoke test passed on port $port."
  }
  finally {
    if ($process -ne $null -and -not $process.HasExited) {
      $process.Kill()
      $process.WaitForExit()
    }
  }
}
finally {
  Pop-Location
}
