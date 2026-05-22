# Publishing to GitHub

## Preflight

Run:

```powershell
.\scripts\smoke-test.ps1
```

Confirm generated files are not included:

```powershell
git status --short
```

Do not commit:

- `bin/`
- `tools/`
- `.runtime*/`
- downloaded `.exe` files

## Create the Repository

```powershell
git init
git add .
git commit -m "Initial WinRemote MVP"
git branch -M main
git remote add origin https://github.com/YOUR_NAME/winremote.git
git push -u origin main
```

## Recommended GitHub Settings

- Enable private vulnerability reporting
- Protect the `main` branch after the first push
- Require the `Build` workflow before merging pull requests
- Add topics such as `remote-desktop`, `windows`, `ios`, `cloudflare-tunnel`, `mjpeg`

## Release Notes

For the first release, mark it clearly as experimental:

```text
WinRemote v0.1.0

Experimental MVP for consensual remote control of a logged-in Windows desktop from iPhone/Safari.
Not production-hardened.
```
