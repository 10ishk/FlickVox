# FlickVox

FlickVox is a lightweight, fully offline Windows text-to-speech app for everyday use, communication, and gaming. It uses Piper only and sends audio through the FlickVox process with NAudio.

## Framework decision

WPF was selected over WinUI 3 for version 0.1.0: it has a smaller, more direct desktop deployment path and mature support for borderless overlay windows, global hotkeys, tray integration, and NAudio. The UI uses original Fluent-inspired WPF styling rather than a UI framework.

## Features

- One compact WPF window: a 360 × 48 quick-speaking pill summoned with `Ctrl+Alt+T`, expandable in place into a small workspace. It keeps the same draft and speech state in both modes.
- Expanded workspace with saved phrase chips (local `Alt+1`–`Alt+9`), voice/output/speed popovers, recent-message popover and cached Repeat. Enter speaks, Shift+Enter inserts a line, and Esc stops or dismisses.
- Piper subprocess integration with cancellation and temporary WAV cleanup.
- Six supported Piper voices: Ryan Low/Medium/High and Lessac Low/Medium/High.
- Voice manager that downloads model and JSON pairs atomically; settings and phrases stored under `%LOCALAPPDATA%\FlickVox`.
- In-app setup guidance can obtain the official Piper runtime when it is missing. Existing valid models are reused.
- NAudio device playback, system tray commands, single-instance protection, and user-level install/uninstall scripts.

## First-time setup

For a normal installation, build and publish the app, then run `scripts\Install-FlickVox.ps1`. It installs the compatible archived Piper Windows runtime and downloads all six supported model/configuration pairs into `%LOCALAPPDATA%\FlickVox\runtime`. Use `-SkipVoices` to defer model downloads and install them later from Voice Manager. Alternatively, the development app guides you through missing Piper/voice setup without installing FlickVox. The installer never writes to a QuickSpeak location.

## Build

Install the .NET 10 SDK, then run:

```powershell
dotnet restore
dotnet publish src/FlickVox/FlickVox.csproj -c Release -r win-x64 --self-contained true
```

Run `scripts\Install-FlickVox.ps1` from the repository root after publishing. `scripts\Uninstall-FlickVox.ps1` retains user data by default; use `-RemoveData` to remove it.

## Audio routing

Select **Default Windows output** or a listed output device. To send speech into Discord or a game as microphone input, route FlickVox through a virtual audio device such as Wave Link and select that virtual input in the other app. Merely selecting speakers does not transmit audio as a microphone. FlickVox does not install drivers, request elevation, or change system audio defaults.

## Screenshots

Screenshots will be added after hardware/UI validation.

## Limitations and manual testing

Global hotkeys may conflict with another application. Focus restoration after hiding the overlay uses normal Windows activation behavior and cannot guarantee exclusive-fullscreen game support. Piper is started per request for reliability; long-message streaming/model persistence is not implemented in 0.1.0. Test the selected Piper build, every voice download, audio devices, scaling, overlay positions, startup integration, and code signing on a normal Windows desktop before release.

## Security and signing

Unsigned binaries can be blocked by Smart App Control. Signing is separate from development: use a trusted certificate or authorized signing service during release publishing. Never commit certificates or signing credentials.
