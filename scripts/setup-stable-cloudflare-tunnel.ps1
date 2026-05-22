param(
  [Parameter(Mandatory = $true)]
  [string]$Hostname,

  [string]$TunnelName = "winremote",
  [int]$Port = 17373
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$cloudflared = Join-Path $root "tools\cloudflared.exe"

if (-not (Test-Path -LiteralPath $cloudflared)) {
  & (Join-Path $PSScriptRoot "download-cloudflared.ps1")
}

Write-Host "Opening Cloudflare login. Use a Cloudflare account that owns $Hostname."
& $cloudflared tunnel login

Write-Host "Creating tunnel: $TunnelName"
& $cloudflared tunnel create $TunnelName

$cloudflaredHome = Join-Path $env:USERPROFILE ".cloudflared"
$credentialFile = Get-ChildItem -Path $cloudflaredHome -Filter "*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $credentialFile) {
  throw "Could not find the tunnel credentials JSON in $cloudflaredHome."
}

$credential = Get-Content -LiteralPath $credentialFile.FullName -Raw | ConvertFrom-Json
$tunnelId = $credential.TunnelID
if (-not $tunnelId) {
  throw "Could not read TunnelID from $($credentialFile.FullName)."
}

$configPath = Join-Path $cloudflaredHome "config.yml"
$credentialPath = $credentialFile.FullName.Replace("\", "/")

@"
tunnel: $tunnelId
credentials-file: $credentialPath
protocol: http2

ingress:
  - hostname: $Hostname
    service: http://127.0.0.1:$Port
  - service: http_status:404
"@ | Set-Content -LiteralPath $configPath -Encoding ASCII

Write-Host "Routing DNS: $Hostname -> $TunnelName"
& $cloudflared tunnel route dns $TunnelName $Hostname

Write-Host ""
Write-Host "Stable tunnel config written to $configPath"
Write-Host "Run this to start it now:"
Write-Host "  tools\cloudflared.exe tunnel run $TunnelName"
Write-Host ""
Write-Host "For always-on use, install WinRemote autostart and install cloudflared as a Windows service from an elevated terminal:"
Write-Host "  .\install-autostart.bat"
Write-Host "  tools\cloudflared.exe service install"
