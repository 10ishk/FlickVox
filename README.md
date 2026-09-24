# FlickVox

FlickVox is a lightweight, fully offline Windows text-to-speech app for everyday use, communication, and gaming. It uses Piper only and sends audio through the FlickVox process with NAudio.

## Framework decision

WPF was selected over WinUI 3 for version 0.1.0: it has a smaller, more direct desktop deployment path and mature support for borderless overlay windows, global hotkeys, tray integration, and NAudio. The UI uses original Fluent-inspired WPF styling rather than a UI framework.

## Features

- Main text-to-speech window with speed, output device, repeat, recent messages, and saved phrases.
- Compact draggable overlay, summoned with `Ctrl+Alt+T`; Enter speaks, Shift+Enter inserts a line, and Esc hides/stops.
- Piper subprocess integration with cancellation and temporary WAV cleanup.
- Six supported Piper voices: Ryan Low/Medium/High and Lessac Low/Medium/High.
- Voice manager that downloads model and JSON pairs atomically; settings and phrases stored under `%LOCALAPPDATA%\FlickVox`.
- NAudio device playback, system tray commands, single-instance protection, and user-level install/uninstall scripts.

## First-time setup

Build and publish the app, then run `scripts\Install-FlickVox.ps1`. Install a compatible Piper Windows executable at `%LOCALAPPDATA%\FlickVox\runtime\piper\piper.exe`. Open **Voice manager** and download a supported model. This source repository does not redistribute Piper or model files pending release-specific license verification.

## Build

Install the .NET 10 SDK, then run:

```powershell
dotnet restore
dotnet publish src/FlickVox/FlickVox.csproj -c Release -r win-x64 --self-contained true
```

Run `scripts\Install-FlickVox.ps1` from the repository root after publishing. `scripts\Uninstall-FlickVox.ps1` retains user data by default; use `-RemoveData` to remove it.

## Audio routing

Select **Default Windows output** or a listed output device. Apps such as Elgato Wave Link work when configured as a normal Windows output route; FlickVox does not install drivers, request elevation, or change system audio defaults.

## Screenshots

Screenshots will be added after hardware/UI validation.

## Limitations and manual testing

Global hotkeys may conflict with another application. Focus restoration after hiding the overlay uses normal Windows activation behavior and cannot guarantee exclusive-fullscreen game support. Piper is started per request for reliability; long-message streaming/model persistence is not implemented in 0.1.0. Test the selected Piper build, every voice download, audio devices, scaling, overlay positions, startup integration, and code signing on a normal Windows desktop before release.

## Security and signing

Unsigned binaries can be blocked by Smart App Control. Signing is separate from development: use a trusted certificate or authorized signing service during release publishing. Never commit certificates or signing credentials.
