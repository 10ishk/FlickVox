# FlickVox v0.1.0 — Initial Preview

FlickVox is a lightweight Windows text-to-speech companion. Press **Ctrl+Alt+T → type → Enter** to speak through Piper; the quick pill can hide immediately while speech continues in the background.

## Highlights

- Unified quick pill and expanded workspace, with a compact overflow for voice selection/preview, speed, output device, saved phrases, recent messages and Repeat.
- Six selectable US English Piper voices: Ryan Low/Medium/High and Lessac Low/Medium/High. Voices and the compatible Piper runtime download through the app; they are not bundled in the ZIP.
- Saved phrases with `Alt+1`–`Alt+9` shortcuts, local history, speech cancellation, cached Repeat, a tray menu and eight named themes plus System mode.
- Output routing to a chosen Windows device, including a virtual audio device already installed by the user.

## Download and setup

Download **FlickVox-v0.1.0-win-x64.zip** and its `.sha256` file below. Verify the checksum, extract the entire ZIP, and run `FlickVox.exe`. The package is self-contained for Windows x64; no separate .NET runtime is needed. On first run, choose **Set up voices**, download Piper and install a voice. The initial downloads require internet access; synthesis and playback operate offline afterward. Settings, phrases, history and downloaded models live under `%LOCALAPPDATA%\FlickVox` and are not in the ZIP. There is no separately verified installer for this preview.

The executable is **unsigned**. Windows may warn about an unknown publisher; do not bypass a security block you do not trust.

## Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+T` | Open the quick pill (default global hotkey) |
| `Enter` / `Shift+Enter` | Speak / insert a newline |
| `Ctrl+E` | Toggle expanded and quick modes |
| `Ctrl+S` / `Ctrl+R` / `Ctrl+L` | Save current phrase / Repeat / Clear |
| `Alt+1`–`Alt+9` | Speak the first nine saved phrases |
| `Esc` | Dismiss overflow, Stop, or hide the window |

Only the summon hotkey is global; the other shortcuts require FlickVox focus.

## Preview limitations

Only the six configured US English voices are supported. A virtual audio device is required to send speech to a chat application's microphone; FlickVox does not install one. Audible speaker output, secondary-device/Discord routing, exclusive-fullscreen games, all DPI configurations and live Windows light/High Contrast tray switching have not been verified on every setup. Windows may cache pinned taskbar icons. The app is not digitally signed.

## License and acknowledgements

FlickVox is MIT licensed and created by **10ishk**. Speech synthesis is powered by [Piper](https://github.com/rhasspy/piper); playback uses [NAudio](https://github.com/naudio/NAudio). The ZIP includes FlickVox's `LICENSE`, `THIRD_PARTY_NOTICES.md` and applicable dependency/font/icon license texts. Voice models are downloaded individually and not redistributed in this package.
