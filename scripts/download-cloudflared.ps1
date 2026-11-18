$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$tools = Join-Path $root "tools"
$target = Join-Path $tools "cloudflared.exe"
$tmp = Join-Path $tools "cloudflared.exe.download"
$url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"

New-Item -ItemType Directory -Force -Path $tools | Out-Null

try {
  Write-Host "Downloading $url"
  Invoke-WebRequest -Uri $url -OutFile $tmp

  $bytes = [System.IO.File]::ReadAllBytes($tmp)
  if ($bytes.Length -lt 5MB -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
    throw "Downloaded file is not a Windows executable. Check the network or proxy and run again."
  }

  Move-Item -LiteralPath $tmp -Destination $target -Force
}
finally {
  if (Test-Path -LiteralPath $tmp) {
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
  }
}

Write-Host "Installed $target"
& $target --version