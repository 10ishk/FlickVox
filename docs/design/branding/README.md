# Local Signal branding

`flickvox-icon.svg` and `flickvox-tray.svg` are the approved vector masters. Their `-light` variants change the dark central mark to soft white while retaining the brass and teal details. Their `-high-contrast` variants are solid white. All SVG geometry is unchanged.

The three `src/FlickVox/Assets/FlickVoxTray*.ico` files contain transparent 16, 20, 24, 32, 40, 48, 64, 128 and 256 px frames rendered from the tray SVGs. At runtime, Windows taskbar appearance (not the FlickVox UI theme) selects light artwork for a dark taskbar, dark artwork for a light taskbar, or solid white in High Contrast. `src/FlickVox/Assets/FlickVox.ico` is a fixed, contrast-safe icon: the approved tray artwork on a pale rounded card with a dark outline. It is used for the EXE, windows and taskbar, which cannot reliably switch icons with Windows appearance.

The application header and About page use the `flickvox-ui-*.png` resources selected by the shared theme manager. `flickvox-icon.png` remains the full-color README/promotional mark. The two 512 px tiles and `flickvox-tray.png` are reference assets; they are not bundled in the application.
