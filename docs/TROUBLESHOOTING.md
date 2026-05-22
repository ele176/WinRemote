# Troubleshooting

## iPhone Cannot Open the Public URL

Make sure the terminal shows:

```text
Registered tunnel connection
```

If you only see the `https://*.trycloudflare.com` URL but not the registered connection message, the tunnel is not ready.

## URL Opens the Wrong Page

Another local service may be using the expected port. `run-public.bat` now chooses a free port automatically. Restart it and use the newly printed URL.

## Cloudflare URL Changes

Quick tunnels are temporary. Restarting `run-public.bat` creates a new URL. Use `setup-stable-tunnel.bat` with a Cloudflare-hosted domain for a stable URL.

## Screen Shows a Placeholder

The Agent cannot capture the screen unless it runs inside an unlocked interactive Windows desktop session. Background jobs, Windows services, locked desktops, and some remote sessions may not expose a capturable desktop.

## Mouse or Keyboard Does Not Work

Windows may block input injection into elevated apps or UAC secure desktop when the Agent is not running elevated. This is expected Windows behavior. Do not try to bypass UAC.

## PowerShell Says Script Execution Is Disabled

Run the batch file instead of the `.ps1` directly, or use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

## Cloudflare Uses QUIC and Fails

The public script forces HTTP/2:

```text
--protocol http2
```

If you run `cloudflared` manually, include that flag.
