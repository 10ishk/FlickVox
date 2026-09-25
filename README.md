<div align="center">
  <img src="src/FlickVox/Assets/Branding/flickvox-icon.png" alt="FlickVox Local Signal icon" width="112">

  <h1>FlickVox</h1>

  <p>A lightweight, offline text-to-speech companion for Windows, powered by Piper.</p>

  <p><strong>Ctrl+Alt+T → Type → Enter → Speak in the background.</strong></p>
</div>

FlickVox is a .NET 10 WPF application that turns typed messages into speech through a chosen Windows output device. The quick pill and expanded workspace are two modes of the same window, so the current draft and playback state stay together. There is no public binary release yet; the instructions below build from source.

## What it does

- Summon the quick pill with a global hotkey; submit with Enter. When **Hide quick pill** is enabled, it hides immediately while speech continues in the background. Reopen it to Stop. FlickVox does not manually restore another application's focus.
- First-time users see a minimal composer, prominent Speak button and **Set up voices** prompt. The prompt opens voice installation and audio configuration. After the first successful speech, the normal header and overflow menu become available. Existing settings migrate to the unlocked view.
- The compact overflow groups voice selection and preview, speed, output device, saved phrases and Save Current, recent messages, and cached Repeat.
- Save phrases locally, edit or delete them, and speak the first nine with `Alt+1`–`Alt+9`. Recent messages and the current draft are separate from saved phrases.
- Select among six supported US English Piper models: Ryan Low/Medium/High and Lessac Low/Medium/High. Voice Manager downloads model/configuration pairs when requested.
- Choose from eight named themes, including Paper, Blush Pink and High Contrast, or follow the system theme. The interface includes keyboard focus indicators, accessible control names and reduced-motion support.
- Use the tray menu to reopen the workspace or pill, stop speech, repeat, open Settings or exit.

## Application screenshots

The screenshot below was captured from the running development build with an empty composer and no personal data.

![FlickVox expanded window with minimal composer and overflow control](docs/screenshots/expanded.jpg)

## Quick start from source

On Windows, install the .NET 10 SDK, clone this repository, then run:

```powershell
dotnet restore src/FlickVox/FlickVox.csproj
dotnet publish src/FlickVox/FlickVox.csproj -c Release -r win-x64 --self-contained true -o src/FlickVox/bin/Release/publish
```

Launch `src/FlickVox/bin/Release/publish/FlickVox.exe`. On a new profile, choose **Set up voices** to obtain Piper and install a voice. The application stores settings, downloaded runtime/voices and saved phrases under `%LOCALAPPDATA%\FlickVox`. It does not require a separate manual Piper installation.

For a per-user installation, run `scripts\Install-FlickVox.ps1` from the repository root after publishing. It installs the compatible Piper runtime and, by default, downloads all six voice/model pairs. `-SkipVoices` defers those model downloads to Voice Manager. The installer uses `%LOCALAPPDATA%\Programs\FlickVox` and does not write to QuickSpeak. `scripts\Uninstall-FlickVox.ps1` retains user data unless `-RemoveData` is explicitly supplied.

## Gaming and voice chat

1. Configure a suitable output in the overflow menu or Settings. For voice chat, choose an existing virtual audio device and select its corresponding input as the microphone in your chat application. FlickVox does not install a virtual device or change Windows audio defaults.
2. Press `Ctrl+Alt+T`, type a message and press Enter. With quick-pill hiding enabled, speech generation and playback continue after the pill disappears.
3. Press the hotkey again to reopen the pill and use Stop if needed. Test your game in its actual display mode: global hotkeys, overlays and focus behavior can differ in exclusive fullscreen or with conflicting shortcuts.

Selecting ordinary speakers only plays locally; it does not send speech to another application's microphone.

## Keyboard reference

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+T` | Summon the quick pill (default global hotkey; configurable) |
| `Enter` | Speak the composer text |
| `Shift+Enter` | Insert a newline |
| `Esc` | Close overflow, Stop speech, or hide the window, in that order |
| `Ctrl+E` | Toggle expanded and quick-pill modes |
| `Ctrl+S` | Save the current phrase |
| `Ctrl+R` | Repeat the last message |
| `Ctrl+L` | Clear the composer |
| `Alt+1`–`Alt+9` | Speak one of the first nine saved phrases |

The quick pill's drag grip can also be double-clicked to expand. Shortcuts that act on the window require FlickVox to have keyboard focus; only the configured summon hotkey is global.

## Voices, audio and development

Voice Manager reports which models are installed, allows selecting an installed voice, and offers download or removal controls. Do not remove a model you still need. Speech uses Piper for synthesis and NAudio for playback. Temporary WAV files are cleaned after playback or cancellation; Repeat uses the cached last message.

The project is WPF rather than WinUI. Source lives in `src/FlickVox`; the design specification and its historical proposals are in `docs/UI_UX_SPEC.md`. The published development executable is not a signed or tested public release.

## Current limitations

- Only six US English Piper voices are configured. Additional voices and streaming synthesis are not implemented.
- A virtual audio device is required for routing speech into a voice-chat microphone. FlickVox does not include one.
- The app is unsigned. Windows security may block an unsigned development executable; do not bypass security protections to run it.
- Hotkeys and overlay visibility may be affected by other software or exclusive-fullscreen games. Hardware-specific audio routing, every DPI configuration and every game have not been exhaustively verified.
- Voice-model redistribution terms vary; this repository does not bundle the voice models.

## License and acknowledgements

FlickVox source is [MIT licensed](LICENSE). Created and maintained by [10ishk](https://github.com/10ishk). Speech synthesis is powered by [Piper](https://github.com/rhasspy/piper); audio playback uses NAudio. Fonts and Fluent icon paths carry their own licenses. See [third-party notices](THIRD_PARTY_NOTICES.md) before redistribution, especially for voice-model terms.
