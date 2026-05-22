# Quick Start

## Requirements

- Windows 10/11
- iPhone with Safari
- An unlocked, logged-in Windows desktop session
- For public access: internet connectivity from both devices

## Local Network Mode

Use this when the iPhone and Windows computer are on the same Wi-Fi:

```powershell
.\run.bat
```

Open the printed `http://电脑IP:端口` URL in iPhone Safari, or enter the pairing code after opening the page.

## Public Mode

Use this when the iPhone is not on the same Wi-Fi:

```powershell
.\run-public.bat
```

The script starts the Windows Agent and then starts a Cloudflare Tunnel.

On the iPhone, open only the `https://*.trycloudflare.com` URL printed by Cloudflare. Do not open `127.0.0.1`; that address is only for the local tunnel process on the Windows machine.

## Controls

- Touch and drag on the screen: move and drag with left mouse button
- `L`: left button mode
- `R`: right click mode
- `2x`: double click last position
- `↑` / `↓`: scroll
- Keyboard icon: open text input controls

## Autostart

Install Agent startup for the current Windows user:

```powershell
.\install-autostart.bat
```

Remove it:

```powershell
.\uninstall-autostart.bat
```

The Agent must run inside the interactive user desktop session. A Windows Service cannot normally capture and control the current user's desktop because of Session 0 isolation.
