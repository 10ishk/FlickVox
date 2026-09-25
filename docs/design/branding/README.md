# Current FlickVox branding — Local Signal

The approved Local Signal mark comes from `flickvox-local-signal-icons.zip`. The original vector masters are `flickvox-icon.svg` and `flickvox-tray.svg`; the dark and light tiles are documentation/reference assets only and are not bundled into the app.

The package is mapped into the application without changing its established resource paths:

| Package asset | Application or documentation destination | Use |
|---|---|---|
| `ico/app-icon.ico` | `src/FlickVox/Assets/FlickVox.ico` | EXE, window and taskbar icon |
| `ico/tray-icon.ico` | `src/FlickVox/Assets/FlickVoxTray.ico` | System tray |
| `png/icon-256.png` | `src/FlickVox/Assets/Branding/flickvox-icon.png` | Header, About and README mark |
| `source/icon-master.svg` | `flickvox-icon.svg` | Editable main master |
| `source/icon-tray.svg` | `flickvox-tray.svg` | Editable small-size master |
| `png-tray/tray-48.png` | `flickvox-tray.png` | Tray preview |

The older horizontal wordmark assets were retired. The product name remains live text beside the mark so it remains readable and accessible across themes.

## Theme-aware in-app variants

The original SVG geometry is unchanged. `flickvox-icon-light.svg` and `flickvox-tray-light.svg` replace only the ink `#1B1B22` with soft white `#F4F5FA`; brass and teal are preserved. The two High Contrast SVGs use solid white on transparency. The bolder tray master is exported at 128 px for the in-app 20 px header and 32 px About mark:

| In-app resource | Theme |
|---|---|
| `flickvox-ui-light.png` | Dark themes |
| `flickvox-ui-dark.png` | Paper and Blush Pink |
| `flickvox-ui-high-contrast.png` | High Contrast |

`ThemeManager` exposes the selected PNG as the shared `Branding.Logo` dynamic resource. The full-color master PNG remains the README/promotional mark; EXE and tray ICOs do not change with themes.
