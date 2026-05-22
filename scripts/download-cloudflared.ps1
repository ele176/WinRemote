$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$tools = Join-Path $root "tools"
$target = Join-Path $tools "cloudflared.exe"
$tmp = Join-Path $tools "cloudflared.exe.download"
$url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"

New-Item -ItemType Directory -Force -Path $tools | Out-Null

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $tmp
Move-Item -LiteralPath $tmp -Destination $target -Force

Write-Host "Installed $target"
& $target --version
