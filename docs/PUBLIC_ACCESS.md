# Public Access

Public access is implemented with Cloudflare Tunnel. The Windows Agent listens on `127.0.0.1`, and `cloudflared` exposes it through an HTTPS URL.

## Temporary Tunnel

```powershell
.\run-public.bat
```

This creates an account-less quick tunnel:

```text
https://example-words-here.trycloudflare.com
```

The URL changes every time you restart the tunnel.

## Important Timing

Cloudflare may print the URL before the tunnel is fully connected. Wait for:

```text
Registered tunnel connection
```

Then open the URL on your iPhone.

## Why HTTP/2 Is Forced

Cloudflare Tunnel may default to QUIC over UDP. Many home, office, carrier, or proxy networks block or break UDP. `run-public.bat` forces HTTP/2 over TCP 443:

```bat
cloudflared tunnel --url http://127.0.0.1:%PORT% --protocol http2
```

This is usually more reliable for public remote access.

## Stable Domain

For long-term usage, configure a named Cloudflare Tunnel with your own domain:

```powershell
.\setup-stable-tunnel.bat winremote.example.com
```

Requirements:

- A Cloudflare account
- A domain hosted by Cloudflare
- Cloudflare Tunnel installed or downloadable by the helper script

After setup, visit:

```text
https://winremote.example.com
```

## Cost Notes

Cloudflare Tunnel can be convenient for an MVP, but production usage should consider Cloudflare terms, reliability, abuse controls, authentication, logging, and long-term operating costs.
