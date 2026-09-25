# FlickVox theme system

FlickVox uses eight palettes: Midnight Violet, Signal Green, Ember, Ocean, Neon Rose, Paper, Blush Pink, and High Contrast. Each palette in `src/FlickVox/Themes/` declares four neutral colors (`Color.Background`, `Color.Surface`, `Color.SurfaceRaised`, `Color.SurfaceHover`) and `Color.Accent`. The shared `ThemeManager` derives semantic brushes for text, borders, focus, selection, buttons, hover, pressed, danger, and speech states. Shared controls in `Theme.xaml` consume these brushes with `DynamicResource`, so no per-theme control templates are needed.

`System` is the default for new settings. It selects Midnight Violet when Windows uses dark app mode and Paper when Windows uses light app mode. Windows High Contrast temporarily selects the High Contrast palette; disabling it returns to the saved choice. Older `Dark` and `Light` values are interpreted as Midnight Violet and Paper without discarding the settings file. Theme changes update open windows and popovers immediately.

Accent buttons choose black or white text for the stronger contrast. Informational text is adjusted to at least 4.5:1 against the surface and essential control borders and focus indicators to at least 3:1. Hover and pressed accents move toward the color that increases contrast with button text. The UI's speaking waveform is a state animation, not an audio-level meter; it stops on cancellation/completion and is static when Windows client-area animation is disabled.

The approved FlickVox image branding remains independent of the theme palettes. This file records the implemented theme architecture.
