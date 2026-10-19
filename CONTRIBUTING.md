# Contributing to AudioBridge

Thanks for considering a contribution. For larger changes, open an issue first to discuss the approach. Keep pull requests focused and describe the user impact, implementation, and any Windows-specific behavior that needs review.

## Development setup

- Use Windows 10 version 2004 or newer (Windows 11 recommended).
- Install the .NET 10 SDK.
- Restore dependencies and run the app with `dotnet run --project .\src\AudioBridge\AudioBridge.csproj`.
- To build release artifacts, use `build\Build-Release.ps1 -Version 1.0.0` on Windows. Release builds may download the official VB-CABLE package for embedding.

## Pull requests

- Explain what changed and why; link related issues.
- Keep unrelated formatting or refactoring out of the change.
- Update the README or third-party notices when user-facing behavior, setup, or distribution changes.
- Include manual verification steps for audio capture, mixing, monitoring, and device setup changes. This repository currently has no automated test suite.
- Do not commit generated `bin/`, `obj/`, `artifacts/`, driver archives, credentials, or personal settings.

By contributing, you agree that your contributions are provided under the project's MIT License.
