# AudioBridge

AudioBridge is a Windows desktop audio router. It captures one running app or the current default desktop playback mix, optionally mixes in a physical microphone, and sends the result to a selected playback endpoint such as VB-CABLE. It does not change Windows' default input or output devices.

## Features

- Capture one running application using Windows per-process loopback, or capture the current default playback device.
- Mix a selected physical microphone with application audio.
- Independent app, microphone, and mixed-output gain controls (0 dB by default).
- Live input and mix meters, plus app, microphone, and output mute controls.
- Optional headphone monitoring with its own device selector and volume. Desktop capture blocks monitoring on the same default playback device to prevent feedback.
- Microphone noise control: off, a lightweight in-process noise gate, or Windows communications voice processing when supported by the chosen device.
- Monochrome WinForms interface. Device selections and levels are stored in `%APPDATA%\AudioBridge\settings.json`.

The noise gate can reduce very quiet speech. Windows voice processing depends on the microphone driver and endpoint. Neither option is RNNoise.

## Requirements

- Windows 10 version 2004 (build 19041) or newer; Windows 11 is supported.
- .NET 10 SDK to build or run from source.
- Internet access and NuGet restore access to build.
- A virtual audio cable endpoint, such as VB-CABLE, for use as a virtual microphone destination.

End users installing the release MSI do not need the .NET runtime or SDK.

## Run from source

```powershell
.\scripts\Run-Dev.bat
```

Or run the project directly:

```powershell
dotnet run --project .\src\AudioBridge\AudioBridge.csproj
```

## Build a release

On Windows with the .NET 10 SDK and WiX Toolset restore access:

```powershell
.\build\Build-Release.ps1 -Version 1.0.0
```

Or double-click `build\Build-Release.bat`. The build publishes a self-contained Windows x64 executable and builds the MSI installer in `artifacts\`. Unless `-SkipDriverPayload` is used, the build downloads and validates the official VB-CABLE package and embeds it in the executable. A temporarily downloaded driver archive is removed from the source tree after the build.

GitHub Actions runs only when you push a version tag such as `v1.0.0`. It builds the release MSI and EXE, then attaches them to a GitHub Release. Regular commits and pushes to `main` do not run a workflow. For each release, create and push a new tag, incrementing one of the first three version numbers (for example, `v1.0.0` then `v1.0.1`).

The MSI is a per-machine Windows Installer package and can be removed through Windows Settings or Control Panel. It is configured for major upgrades: each published version must have a higher three-part version (for example, `1.0.0` to `1.1.0`) and must keep the existing `UpgradeCode` in `installer/AudioBridge.Installer.wxs`. The installer then replaces the older version; attempts to install an older version over a newer one are blocked.

## Install and configure

1. Install `AudioBridge-Setup.msi` and launch AudioBridge from the Start menu.
2. The MSI installs AudioBridge. On first launch, if VB-CABLE is not detected, AudioBridge offers to unpack the official driver archive embedded in the release executable and launch VB-Audio's installer. The driver is installed by that separate vendor installer, with Windows approval, and may require a restart.
3. Select **One app** and a running application, or select **Desktop audio**. Use **Refresh** if the app was opened after AudioBridge.
4. Select a physical microphone and the desired playback destination. Input gains start at 0 dB.
5. In the target voice or chat application, select the VB-CABLE recording endpoint (often named `CABLE Output (VB-Audio Virtual Cable)`).
6. Click **Start routing**.

AudioBridge displays the destination as **AudioBridge Virtual Mic (VB-CABLE)**. Windows and other applications may show the vendor's original endpoint names.

## Project structure

```text
.github/workflows/   GitHub Actions release workflow
build/               Release build scripts
installer/           WiX MSI installer project
scripts/             Developer launch helper
src/AudioBridge/     WinForms application source and assets
```

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency and VB-CABLE attribution. VB-CABLE is a separate VB-Audio product, not code authored by AudioBridge. Review the vendor's current terms before redistributing it: [VB-Audio licensing](https://vb-audio.com/Services/licensing.htm).

## Limitations and release checks

AudioBridge uses Windows shared-mode WASAPI with a fixed buffer configuration; it cannot promise the same latency as a direct physical microphone. Desktop capture targets the current default playback endpoint. A virtual audio endpoint is required because ordinary Windows APIs do not let an application invisibly inject audio into another physical microphone device.

Before relying on a release, build on Windows and verify app capture, desktop loopback, microphone capture, output monitoring, voice processing availability, and driver install/cancel/restart paths. Unsigned binaries may display Windows SmartScreen warnings.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Please report security issues privately using GitHub's repository security advisories when enabled.

## License

AudioBridge source code is released under the MIT License. See [LICENSE](LICENSE). Third-party software and driver packages have their own terms, described in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).


