# Changelog

## Unreleased

- Added Windows Agent MVP with MJPEG screen streaming and mouse/keyboard input.
- Added iPhone/Safari control UI.
- Added pairing code and token-based request authorization.
- Added Cloudflare Tunnel public-access helper using HTTP/2.
- Added startup scripts, smoke test, and open-source project metadata.

### Fixed

- MJPEG stream loops now stop when the phone disconnects instead of capturing forever.
- Controller input requests are serialized so mouse events arrive in order.
- A failed status request only signs the phone out on HTTP 401, not on a network blip.

### Changed

- README reduced to essentials; machine-specific paths removed.
- Request body, header, text injection, and wheel delta sizes are bounded.
- The cloudflared downloader rejects payloads that are not Windows executables.
- Smoke test now covers unauthorized and wrong-pairing-code responses.
