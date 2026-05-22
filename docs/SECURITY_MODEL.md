# Security Model

## Intended Use

WinRemote is for controlling your own Windows computer, or computers you are explicitly authorized to administer.

## Trust Boundary

The Windows Agent is trusted local software. Anyone who obtains a valid token or paired browser session can control the visible Windows desktop while the Agent is running.

## Current Protections

- Random token generated on Agent startup
- 8-digit pairing code
- Pairing failure lockout after repeated wrong codes
- Control APIs require a token
- Public mode binds the Agent to `127.0.0.1`
- Cloudflare provides HTTPS from iPhone to Cloudflare edge
- Basic browser security headers

## Current Gaps

- No user accounts
- No device whitelist
- No audit log
- No end-to-end identity model
- No per-action approval
- No native iOS keychain integration
- No WebRTC DTLS/SRTP media transport

## Non-Goals

WinRemote will not add features for:

- Stealth operation
- Credential theft
- Unauthorized persistence
- UAC secure desktop bypass
- Windows login or lock-screen bypass
- Antivirus or EDR evasion

## Production Hardening Ideas

- Account login and device binding
- Short-lived pairing codes
- Revocable device sessions
- Audit log with user-visible connection state
- Mandatory TLS origin authentication
- WebRTC transport with TURN only as fallback
- Signed Windows releases
- Automatic update with signature verification
