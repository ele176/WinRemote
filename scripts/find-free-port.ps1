param(
  [int]$Preferred = 17373,
  [int]$Max = 17473
)

$ErrorActionPreference = "Stop"

for ($port = $Preferred; $port -le $Max; $port++) {
  $listener = $null
  try {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
    $listener.Start()
    $listener.Stop()
    Write-Output $port
    exit 0
  }
  catch {
    if ($listener -ne $null) {
      try { $listener.Stop() } catch {}
    }
  }
}

throw "No free TCP port found between $Preferred and $Max."
