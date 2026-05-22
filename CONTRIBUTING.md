# Contributing

Thanks for helping improve WinRemote.

## Development Setup

WinRemote currently builds with the .NET Framework compiler that ships with Windows:

```powershell
.\build.bat
.\scripts\smoke-test.ps1
```

No NuGet packages are required for the current MVP.

## Pull Request Guidelines

- Keep changes focused and easy to review.
- Do not commit generated files from `bin/`, downloaded binaries from `tools/`, or `.runtime*` logs.
- Include a short explanation of the behavior change.
- For security-sensitive changes, describe the risk model and how you tested it.
- Run `.\scripts\smoke-test.ps1` before opening a PR.

## Safety Rules

This project is for consensual remote access to devices you own or are authorized to administer.

Do not contribute features that hide the agent, bypass Windows security boundaries, weaken authentication, persist without user visibility, or evade security software.
