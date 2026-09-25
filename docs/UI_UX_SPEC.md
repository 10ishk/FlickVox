# FlickVox — UI/UX Design Specification

**Version:** 1.0 · **Date:** 24 Sep 2026
**Based on:** the `10ishk/FlickVox` README (WPF · .NET 10 · Piper · NAudio) and your two screenshots (wide and narrow window).
**Goal:** turn FlickVox from "a working tool with default-ish styling" into a product that feels fast, calm, trustworthy and unmistakably designed.

**Implementation status (25 Sep 2026):** This document preserves the original design research and proposals. It is **not** a current feature checklist. The [README](../README.md) and application source describe shipped behavior. Sections 0–8 and 11–24 contain historical ideas, including controls and shortcuts that were never implemented. The current unified-window behavior is summarized immediately below; the older Section 9 text remains for design history.

| Current implementation | Historical proposal, not current behavior |
|---|---|
| One shared quick pill / expanded composer; immediate quick-pill hiding while speech continues; no manual focus restoration | Separate overlay, hiding only after playback, and focus-restoration promises |
| Minimal first-run input, Speak and Set up voices; overflow unlocked after successful speech; returning settings migrated | Three-step skippable welcome wizard and starter phrase packs |
| Overflow menu with collapsible Voice, Speed, Output and Saved sections plus Repeat | Permanently visible footer chips, separate Recent popover and Save chip |
| `Alt+1`–`Alt+9` local phrase shortcuts; `Ctrl+E` mode toggle | `Ctrl+1`–`Ctrl+9`, `Ctrl+,`, F1 cheat sheet and global phrase shortcuts |
| Eight named themes plus System, semantic colors, 16 px vector controls, preparing pulse and playback-only waveform | Fixed dark-only palette, 20 px icons and no activity animation |

**Verification boundary:** the original timing targets, mockups and aspirational accessibility checklist are design goals, not measured release claims. Hardware/game-specific behavior still requires manual verification.

---

## 0. TL;DR — the 12 decisions

| # | Decision | Choice |
|---|---|---|
| 1 | Design direction | **Calm, dark-first "signal" UI**; light theme + follow-system supported |
| 2 | Hero surface | One shared composer in the quick pill and expanded workspace |
| 3 | Primary action | **White `#F4F5F7`** with dark `#0E1015` icon/text |
| 4 | Live colour | **Green `#3DDC97`**, used only during actual playback |
| 5 | Neutrals | Base `#0E1015` → Raised `#14171E` → Hover `#1A1D26` |
| 6 | UI font | **Inter** (bundled static weights) → fallback Segoe UI Variable / Segoe UI |
| 7 | Wordmark font | **Sora SemiBold** (wordmark only) |
| 8 | Accessibility font option | **Atkinson Hyperlegible** as a user-selectable composer font |
| 9 | Icons | **Fluent System Icons** (MIT), 16 px in the current compact interface |
| 10 | Expanded layout | Slim title bar and composer, with advanced controls in compact overflow |
| 11 | Configuration | Voice / Output / Speed in the overflow; also available in Settings |
| 12 | Unified window | One window: quick pill or compact expanded workspace |

If you only have one evening: do **Section 2 (fix the audit list)** and **Phase 0** in Section 19. That alone removes everything that looks "unfinished".

---

## 1. Product understanding

### 1.1 What FlickVox is
A lightweight, **fully offline** Windows text-to-speech app for everyday communication and gaming. Type → Enter → it speaks. A global hotkey (`Ctrl+Alt+T`) summons a compact overlay from anywhere. Audio is routed through NAudio to any output device (e.g. Elgato Wave Link, a virtual cable for voice chat).

### 1.2 Who it is for (assumptions — validate with 3–5 real users)

| Persona | Situation | What they need most |
|---|---|---|
| **The non-speaking / speech-impaired communicator** | Uses TTS as their voice, all day | Reliability, instant repeat, saved phrases, big readable text |
| **The gamer / streamer** | Types into voice chat while playing | Overlay that appears and disappears in < 1 s, never steals more than a second of focus, right output device |
| **The "can't speak right now" user** | Laryngitis, shared room, night-time, noisy place | Zero-setup, natural-sounding voice |
| **The anxious / neurodivergent speaker** | Prefers typing to speaking on calls | Calm UI, no surprises, predictable behaviour |

### 1.3 Jobs to be done
1. **Say something right now** (intent → audio in about a second).
2. **Say it again** (Repeat must feel instant).
3. **Say my common things without typing** (saved phrases).
4. **Sound the way I want** (voice, speed, output device) — *set once, rarely touched*.

### 1.4 North-star UX metrics (targets, measure them)
- Hotkey → cursor blinking in overlay: **≤ 150 ms**
- Enter → visible "working" feedback: **≤ 100 ms**
- Enter → first audio: **as low as Piper allows** (README says Piper starts per request, so cache and warm-up matter; see §16 and §19)
- Repeat → audio: **near-instant** (cache the last synthesized WAV instead of re-running Piper)

### 1.5 Design principles
1. **Speed is the feature.** Every decision is judged by "does this make Enter → audio faster or clearer?"
2. **Keyboard-first, mouse-friendly.** Every action has a shortcut; every shortcut is discoverable.
3. **Calm and dignified.** For some users this *is* their voice. No neon-gamer styling, no toy feel, no jargon.
4. **Always show state.** Ready / Preparing / Speaking / Error — visible within 100 ms, never ambiguous.
5. **Progressive disclosure.** Rare settings hide behind chips and flyouts; the composer owns the screen.
6. **Never in the way.** The overlay is small, predictable, remembers its position, and gets out of the way after speaking.
7. **Accessible by default.** Assume low vision, motor difficulty and screen-reader users are *core* users, not edge cases.
8. **Trust through honesty.** "Offline — nothing leaves your PC" is a feature; say it.

---

## 2. Audit of the current UI (from your screenshots)

| # | What I see | Why it matters | Fix | Priority |
|---|---|---|---|---|
| 1 | **Mixed themes**: white window background and light Windows title bar wrapped around dark cards | Looks unfinished; the wordmark is black-on-white while everything else is dark | Set a root `Background` on the Window, enable dark title bar (DWM immersive dark mode), optionally Mica (§18.2) | **P0** |
| 2 | **Dark text on dark surfaces**: "Saved phrases" and "Recent messages" headings, the Voice/Output combo text, and the "Ready" label are near-invisible | Fails contrast (well under 4.5:1), unreadable | Set `TextElement.Foreground` at the window root and restyle ComboBox/ComboBoxItem/CheckBox (§18.3) | **P0** |
| 3 | Voice dropdown shows `VoiceDefinition { Id = en_US-ryan-medium, DisplayNam…` | This is a C# record's `ToString()` leaking into the UI | Bind to `DisplayName` with an `ItemTemplate` (or override `ToString`); show "Ryan · Medium" (§18.4) | **P0** |
| 4 | **Narrow window**: bottom card is cut off and the footer bar sits on top of it; no scrolling | Content is unreachable | Grid rows `Auto / * / Auto`, content in a `ScrollViewer`, footer as its own row | **P0** |
| 5 | **Narrow window**: blank white band above the logo and a full-width hairline crossing the header | Looks like a rendering bug | Inspect with Live Visual Tree — most likely a leftover Border/Separator or a `WindowChrome` caption area | **P0** |
| 6 | Footer reads "Hide overlay after speakingEsc stops speech" (no gap) | Two items collide | Separate them: checkbox left, shortcut hint right (or move the checkbox into Settings) | P1 |
| 7 | Settings card is a fixed ~420 px wide, truncating the voice name and output device | Truncation hides the one thing users need to verify | Replace with 3 flexible chips (§9.3) | P1 |
| 8 | Clear "✕" button floats mid-bottom of the composer | Ambiguous position/purpose | Right-aligned, icon button, only visible when there is text, tooltip "Clear (Ctrl+L)" | P1 |
| 9 | `↗` button top-right has no label | Users can't guess it opens the overlay | Icon + label "Overlay" with tooltip and the hotkey | P1 |
| 10 | "Ctrl + Alt + T" is a pill of low-contrast text; in the narrow shot it is light grey on white | Reads as a disabled button | Render as **keycaps** `Ctrl` `Alt` `T`, informational, clickable to rebind | P1 |
| 11 | "Ready" pill: label sits above the dot's centre line and is dark on dark blue | Misaligned + illegible | 28 px pill, dot + label centred, label in `Text` colour | P1 |
| 12 | Wide window: the composer stretches ~1850 px | Long line lengths are hard to scan; feels empty | Keep the compact content width even when resized wider (§9) | P1 |
| 13 | Native-looking slider thumb and checkbox in a custom dark UI | Style breaks | Restyle Slider/CheckBox/ComboBox with the token set (§11) | P2 |
| 14 | Four buttons of near-equal weight; **Stop** visible even when idle | Weak hierarchy | One primary (Speak↔Stop), one secondary (Repeat); move "Preview voice" into the voice flyout | P2 |
| 15 | Every region is the same heavy dark card with a strong outline | No hierarchy, feels boxy | Fewer boxes; 1 px subtle strokes; the composer gets the emphasis | P2 |
| 16 | Empty Saved/Recent cards take ~25 % of the window | Wasted space for a first-run screen | Collapsible section with conditional empty states (§9) | P2 |

---

## 3. Brand & personality

**Name logic:** *Flick* = quick, light, a small gesture. *Vox* = voice. The product promise is **"a flick of the keys and it speaks."**

**Personality:** quick · calm · confident · warm. (Think Raycast's speed, Linear's restraint, Windows 11's familiarity.)

**Voice & tone (copy):** short, plain, kind. No exclamation marks, no jokes in error states. Talk like a helpful person, not a system (§17).

**Tagline options**
- *Type it. Say it.*
- *Your voice, one keystroke away.*
- *Speak in a flick.*

**Logo / icon concept**
- Shape: rounded-square (squircle) tile.
- Glyph: three to five **vertical rounded bars** (a waveform) in white; the last bar is **tilted** like a flick of a finger. Optionally the bars sit inside a speech-bubble tail at the bottom-left.
- Fill: **Signal gradient** `#6C63FF → #3DDBB5` at 135°. Use it **only** on the logo, the speaking waveform and onboarding hero. Never on buttons or text.
- Deliverables: multi-size `.ico` (16, 20, 24, 32, 48, 64, 128, 256), a **monochrome tray icon** (light and dark variants), and a tray "speaking" variant (bars animated or filled mint).
- Wordmark: `Flick` in Sora SemiBold `Text` colour + `Vox` in Sora SemiBold `AccentText`. Or all one colour with the bars replacing the "o".

---

## 4. Colour system

### 4.1 Why this palette
- The current blue is very close to a generic default/system blue. **Indigo** is calmer and more ownable, and doesn't clash with the user's Windows accent.
- **Mint** signals "live audio / all good" and is the *only* colour that appears while FlickVox is speaking, which makes state glanceable in peripheral vision (great over a game).
- Cool ink neutrals with a slight blue undertone keep long sessions easy on the eyes and let indigo and mint feel intentional.

### 4.2 Dark theme (default)

| Token | Hex | Use | WPF key |
|---|---|---|---|
| Canvas | `#0E1015` | Unified-window background | `Brush.Canvas` |
| Surface / SurfaceRaised | `#14171E` | Input, popovers, supporting cards | `Brush.SurfaceRaised` |
| SurfaceHover | `#1A1D26` | Chips and row hover | `Brush.SurfaceHover` |
| StrokeSubtle / StrokeControl | `#262A34` | Hairlines and borders | `Brush.StrokeControl` |
| Text | `#E8EAF0` | Primary text | `Brush.Text` |
| TextSecondary | `#9096A3` | Footer and icons | `Brush.TextSecondary` |
| TextTertiary | `#5B6070` | Placeholder and muted text | `Brush.TextTertiary` |
| PrimaryAction | `#F4F5F7` | Send button while idle | `Brush.PrimaryAction` |
| OnPrimary | `#0E1015` | Send icon | `Brush.OnPrimary` |
| Signal | `#3DDC97` | Actual playback only | `Brush.Signal` |
| SignalBorder | `#1F5A43` | Speaking outline | `Brush.SignalBorder` |
| Danger | `#F0616D` | Errors, destructive | `Brush.Danger` |
| Warning | `#F2B441` | Hotkey conflict, model missing | `Brush.Warning` |

### 4.3 Light theme

| Token | Hex |
|---|---|
| Canvas | `#F3F5F9` |
| Surface | `#FFFFFF` |
| SurfaceRaised | `#EEF1F6` |
| SurfaceHover | `#E5E9F0` |
| StrokeSubtle | `#E1E5EC` |
| StrokeControl | `#7A8599` |
| Text | `#121826` |
| TextSecondary | `#4A5568` |
| TextTertiary | `#66717F` |
| Accent / Hover / Pressed | `#4F46E5` / `#4338CA` / `#3730A3` |
| AccentText | `#4338CA` |
| Signal (text-safe) | `#0B7A63` |
| Danger (text-safe) | `#C0392B` |
| Warning (text-safe) | `#8A5A00` |

### 4.4 Measured contrast (WCAG 2.x, computed)

| Pair | Ratio | Verdict |
|---|---|---|
| Text on Canvas (dark) | 16.6 : 1 | AAA |
| Text on Surface (dark) | 15.4 : 1 | AAA |
| TextSecondary on Surface (dark) | 8.2 : 1 | AAA |
| TextTertiary on Surface (dark) | 5.5 : 1 | AA |
| TextTertiary on SurfaceRaised (dark) | 5.1 : 1 | AA |
| White on Accent `#4F46E5` | 6.3 : 1 | AA (AAA large) |
| White on AccentHover `#5B54F0` | 5.3 : 1 | AA |
| AccentText `#A5ADFF` on Surface | 8.3 : 1 | AAA |
| Mint on Surface | 10.0 : 1 | AAA |
| Danger `#FF7A7A` on Surface / Raised | 6.9 / 6.3 : 1 | AA |
| Warning on Surface | 9.9 : 1 | AAA |
| Focus ring `#A5ADFF` on Canvas | 9.0 : 1 | Exceeds 3:1 |
| StrokeControl on Surface / Raised (dark) | 3.6 / 3.3 : 1 | Meets 3:1 for UI components |
| Light: Text on white | 17.7 : 1 | AAA |
| Light: TextTertiary on white | 5.0 : 1 | AA |
| Light: StrokeControl on white | 3.7 : 1 | Meets 3:1 |

> Rule of thumb: text ≥ 4.5 : 1, large text and control borders/icons ≥ 3 : 1.

### 4.5 Usage rules
- **90 / 10:** neutrals fill ~90 % of the UI; accent + signal are ≤ 10 %.
- **One accent-filled button per view** (Speak). Everything else is subtle/ghost.
- **Mint is semantic, never decorative.** It means "audio is live / ready / success".
- On dark, use `AccentText` (not `Accent`) for *text*. On light, use `AccentText #4338CA`.
- Never rely on colour alone: pair state colour with an icon or label (Ready · Speaking · Error).

### 4.6 State → colour map

| State | Dot / icon | Label | Extra |
|---|---|---|---|
| Ready | Mint dot | "Ready" | — |
| Preparing voice | Accent pulsing dot | "Preparing…" | Speak button shows spinner |
| Speaking | Mint **waveform** | "Speaking" | 1 px mint ring on composer/overlay, Speak morphs to **Stop** |
| Downloading voice | Accent progress bar | "Downloading 43 %" | Cancel action |
| Warning | Amber triangle | Specific message | Action button |
| Error | Coral circle-x | Specific message | Action button |

---

## 5. Typography

### 5.1 Families

| Role | Font | Notes |
|---|---|---|
| UI | **Inter** (Regular 400, Medium 500, SemiBold 600) | OFL licence. **Bundle static TTFs** — WPF does not reliably support OpenType variable-font axes. |
| UI fallback | Segoe UI Variable Text → Segoe UI | Variable ships with Windows 11 only |
| Wordmark | **Sora SemiBold** | Wordmark/onboarding only |
| Accessibility option | **Atkinson Hyperlegible** | Optional composer/overlay font in Settings; designed for legibility |
| Keycaps | Inter SemiBold 12 | No monospace needed |

WPF font reference (folder path must end with `/` before `#`):
```xml
<FontFamily x:Key="Font.UI">pack://application:,,,/Assets/Fonts/#Inter, Segoe UI Variable Text, Segoe UI</FontFamily>
<FontFamily x:Key="Font.Brand">pack://application:,,,/Assets/Fonts/#Sora, Segoe UI Semibold</FontFamily>
```
Set `TextOptions.TextFormattingMode="Ideal"` and `TextOptions.TextRenderingMode="ClearType"` on the root; `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`.

### 5.2 Type scale (DIPs)

| Style | Size / line | Weight | Where |
|---|---|---|---|
| Wordmark | 24 / 32 | Sora 600 | Header |
| Title | 20 / 28 | 600 | Dialog and page titles |
| **Composer** | **22 / 32** (user range 16–40) | 400 | Main composer text |
| Overlay input | 20 / 28 | 400 | Overlay |
| Section header | 13 / 18 | 600, `TextSecondary` | "Saved", "Recent", "Voice" |
| Body | 14 / 20 | 400 | Lists, labels |
| Body strong | 14 / 20 | 500 | Chip values, list titles |
| Button | 14 / 20 | 600 | Buttons |
| Caption | 12 / 16 | 400 | Hints, helper text (minimum size — never smaller) |
| Keycap | 12 / 16 | 600 | Shortcuts |

Rules: sentence case everywhere; max line length in lists ~60 characters; never use light weights on dark backgrounds below 16 px.

---

## 6. Spacing, shape, elevation, icons

**Spacing (4-pt grid):** 4 · 8 · 12 · 16 · 24 · 32 · 48. Outer window padding 24, gap between blocks 16, gap inside groups 8.

**Radius**

| Token | px | Use |
|---|---|---|
| `Radius.XS` | 6 | Keycaps, small badges |
| `Radius.S` | 10 | Buttons, inputs, chips |
| `Radius.M` | 14 | Cards, flyouts |
| `Radius.L` | 18 | Composer, overlay |
| `Radius.Pill` | 999 | Status pill, segmented control |

**Elevation**

| Level | Treatment | Use |
|---|---|---|
| 0 | Canvas | Window |
| 1 | Surface + 1 px StrokeSubtle | Cards, composer |
| 2 | SurfaceRaised + 1 px StrokeSubtle + shadow `0 8 24 #59000000` | Flyouts, menus, dialogs |
| 3 | OverlayBg + 1 px `#1AFFFFFF` + shadow `0 12 40 #80000000` | Overlay window |

Prefer 1 px strokes over thick outlines; use the shadow only on floating things.

**Icons:** Fluent System Icons (Regular, 20 px; 16 px in dense lists; 24 px in empty states). Export as XAML `Geometry`/`DrawingImage` so you don't depend on the Segoe Fluent Icons font (Windows 11 only). Set: play, stop, repeat, dismiss, bookmark/star, window-arrow (overlay), settings, speaker, speed/gauge, person-voice, arrow-download, checkmark, warning, more-horizontal, re-order dots (drag handle), search, chevron-down. Every icon-only button needs a tooltip **and** an `AutomationProperties.Name`.

---

## 7. Information architecture & screens (historical proposal)

```
FlickVox
├─ Unified primary window
│   ├─ Quick pill (global hotkey): shared composer + Send/Stop
│   └─ Expanded workspace: title controls · saved chips · quiet configuration footer · Recent popover
├─ Voice Manager (dialog)
├─ Settings (dialog): General · Voice & audio · Compact window · Appearance · Accessibility · About
└─ Tray menu
```

No separate overlay process or competing primary window exists. Both modes share one text box and service graph.

| Screen | Purpose | Entry | Exit |
|---|---|---|---|
| Unified window (expanded) | Compose, speak, manage phrases | Launch / tray click / expand pill | Collapse / close to tray |
| Same window (quick pill) | Speak without leaving what you're doing | `Ctrl+Alt+T` / tray | Expand / Esc / after successful playback |
| Voice Manager | Install, preview, remove voices | Voice flyout → "Manage voices…" | Close |
| Settings | Rare configuration | Gear, `Ctrl+,` | Close / Esc |

---

## 8. Keyboard & interaction model (historical proposal)

| Shortcut | Scope | Action |
|---|---|---|
| `Ctrl+Alt+T` | Global | Show/hide overlay (rebindable, conflict-checked) |
| `Enter` | Composer / overlay | Speak |
| `Shift+Enter` | Composer / overlay | New line |
| `Esc` | App | 1st press: stop speaking. If idle: hide overlay / clear focus |
| `↑` (composer empty) | Composer / overlay | Recall previous message (`↓` goes forward) |
| `Ctrl+R` | Main | Repeat last message |
| `Ctrl+S` | Composer | Save current text as a phrase |
| `Ctrl+L` | Composer | Clear |
| `Ctrl+1` … `Ctrl+9` | Main / overlay | Speak saved phrase #n instantly |
| `Ctrl+,` | Main | Open Settings |
| `F1` or `?` | Main | Show shortcut cheat-sheet |

Interaction rules:
- **Autofocus the composer** whenever the window or overlay is activated. Users should never have to click before typing.
- **Single click** on a *saved phrase* speaks it (that's the AAC convention); **single click** on a *recent message* loads it into the composer; **double-click** or the ▶ button speaks it. Hover reveals ▶ / ☆ / ⋯.
- Never lose typed text: Esc from the overlay keeps the draft for 60 s.
- Destructive actions (delete phrase, remove voice) use **Undo toast** instead of a confirmation dialog, except deleting a voice model (confirm, since it re-downloads).

---

## 9. Unified compact window (current implementation)

FlickVox has one primary WPF window with two modes, not a separate main window and overlay. The same text box, draft, speech state, shared history, phrase service, hotkey and tray commands serve both modes. Settings and Voice Manager remain separate supporting dialogs.

### Collapsed quick pill

Target approximately 360 × 48 DIPs for returning users, with an adjustable width. A six-dot drag grip, shared input and Send/Stop button fit in one pill. `Ctrl+Alt+T` summons this mode and focuses the input; `Ctrl+E` or a double-click on the grip expands it. Enter speaks; Shift+Enter inserts a newline. When the hide preference is enabled, the pill hides **immediately on submission** while Piper and playback continue. Errors retain the submitted text. The grip and a transient preparing/playback indicator are the only other visible controls; first-run users also see a setup prompt.

### Expanded workspace

Target roughly 380–440 DIPs wide and a short content-driven footprint. The slim title bar has the approved logo, a compact overflow action, Pin, Settings, Collapse and Close-to-tray controls. The composer remains the same input as the quick pill. Saved phrases, Save Current, Recent, Voice, Output, Speed and Repeat are in one compact overflow popup, with collapsible groups and one outer scroll region. The first nine phrases have local `Alt+1`…`Alt+9` shortcuts; right-click actions provide Edit/Rename and Delete. Repeat uses the cached last message when available. Before the first successful speech, only the composer, prominent Speak action and setup prompt are shown, plus essential drag/close affordances.

The expanded/collapsed state and position persist. A shortcut invocation deliberately opens the quick pill even if the saved state was expanded. Old overlay coordinates and non-default size settings migrate without replacing personal settings.

### Speech state and palette

Idle and Typing use neutral feedback; Preparing uses a subtle reduced-motion-aware pulse while Piper generates; a lightweight waveform appears **only during actual playback**; Error uses the error token. Send changes to Stop while speech is active. All colors are selected through semantic resources for the active theme rather than a fixed dark-only palette.

The original Signal color values elsewhere in this document are historical references. The application currently offers eight named palettes plus System mode, including Paper, Blush Pink and High Contrast. The active palette feeds shared semantic resources for text, surfaces, buttons, focus, status and supporting windows.

[Unified layout reference](design/flickvox_compact_layout_proposal.html) and [palette reference](design/flickvox_color_palette.html) are visual source material, not HTML used in the WPF application. The earlier [compact-first concept](design/flickvox_compact_concept.svg) is superseded for the primary-window architecture.

---

## 10. Interaction and accessibility (historical proposal)

The global shortcut registers once and opens the existing primary window in quick mode; no second primary window or additional global hooks are created. Alt+1…Alt+9 shortcuts are local to FlickVox focus and announce their mapping in the Saved label and phrase tooltips. The same phrase and recent collections back both window modes. Popovers and dialogs remain keyboard accessible. Positioning clamps to the current visible working area and respects DPI scaling; exclusive-fullscreen games may still prevent an overlay from appearing.

---
## 11. Component specifications

### 11.1 Buttons

| Type | Fill | Text / icon | Border | Size | Use |
|---|---|---|---|---|---|
| **Primary** | `Accent` | White | none | 44 h, radius 10, px 20 | Speak, Download, Save |
| **Secondary** | `SurfaceRaised` | `Text` | 1 px `StrokeControl` | 40 h | Repeat, Overlay |
| **Ghost** | transparent | `TextSecondary` → `Text` on hover | none | 36–40 h | Cancel, links, row actions |
| **Icon** | transparent | `TextSecondary` | none | 36 × 36, icon 20 | Clear, Save, Settings |
| **Danger** | `Danger` @ 16 % | `Danger` | none | 40 h | Remove voice (confirm only) |

States (all types): **hover** +1 step lighter (`AccentHover` / `SurfaceHover`) · **pressed** darker + scale 0.98 (80 ms) · **focus** 2 px `AccentText` ring with 2 px offset (keyboard only) · **disabled** 40 % opacity, no hover · **busy** spinner replaces icon, label stays.

### 11.2 Other controls

| Component | Spec |
|---|---|
| **Keycap** | h 20, min-w 20, px 6, radius 6, `SurfaceRaised`, 1 px `StrokeSubtle` + 2 px bottom border for depth, 12/16 SemiBold, one cap per key (`Ctrl` `Alt` `T`) |
| **Status pill** | h 28, radius pill, `SurfaceRaised`; 8 px state dot/waveform + 12–13 px label, vertically centred; announces changes to screen readers |
| **Segmented control** | Pill track `SurfaceRaised`; selected segment `Surface` + 1 px `StrokeSubtle`, label `Text`; unselected `TextSecondary` |
| **Chip (voice bar)** | h 36, radius 10, icon 16 + `TextSecondary` 12 caption ("Voice") over `Text` 14 medium value, chevron; max width 220 with ellipsis |
| **Toggle switch** | 40 × 20 track; off `SurfaceRaised` + `StrokeControl` border; on `Accent`; thumb white 14 px; label to the left, control to the right |
| **Checkbox** | 20 × 20, radius 6, `StrokeControl` border; checked `Accent` fill + white check |
| **Text field** | h 36, `SurfaceRaised`, radius 10, 1 px `StrokeControl`; focus 2 px `AccentText` |
| **List item** | See §9.4 |
| **Toast** | Bottom-centre of window, 360 w, elevation 2, icon + message + optional action ("Undo"), auto-dismiss 4 s, pausable on hover, `role=alert` for errors |
| **Dialog** | Max 560 w (Voice Manager 720), radius 14, elevation 2, scrim `#99000000`, title 20/28, actions right-aligned (primary rightmost), Esc closes |
| **Tooltip** | 600 ms delay, `SurfaceRaised`, 12 px, includes shortcut ("Overlay · Ctrl+Alt+T") |
| **Progress bar** | 4 px, track `SurfaceRaised`, fill `Accent`; determinate for downloads, indeterminate only for "Preparing…" |

---

## 12. Voice Manager

Dialog 720 × 560. Purpose: install/preview/choose voices without confusion.

```
┌───────────────────────────────────────────────────────────────┐
│ Voices                                                    ✕   │
│ All voices run on your PC. Nothing is sent online.            │
├───────────────────────────────────────────────────────────────┤
│ Ryan · US English                                             │
│  ● Low     Fastest, smallest    ~xx MB   [ Installed ]  ▶ ⋯   │
│  ● Medium  Balanced  (default)  ~xx MB   [ Installed ]  ▶ ⋯   │
│  ○ High    Most natural         ~xx MB   [ Download ]         │
│                                                               │
│ Lessac · US English                                           │
│  ○ Low …                                                      │
├───────────────────────────────────────────────────────────────┤
│                                          [ Close ]            │
└───────────────────────────────────────────────────────────────┘
```

- **Group by voice family** (Ryan, Lessac), then quality rows — six rows total, far easier to scan than a flat list. Show real file sizes from your download manifest.
- Quality copy: **Low** = fastest/smallest · **Medium** = balanced · **High** = most natural/largest. Confirm the gender/accent descriptors against Piper's voice list before shipping.
- Row states: *Installed* (check + ▶ preview + ⋯ → Set as default / Remove) · *Not installed* (**Download** secondary button) · *Downloading* (determinate bar, %, **Cancel**) · *Failed* (coral text "Download failed — Retry"; your atomic download means no half-installed models).
- **Preview** speaks a fixed friendly sample ("Hi, this is how I sound.") — and previews without requiring the voice to be selected.
- The currently selected voice has an `AccentSubtle` row and a "Default" tag.
- Removing a voice needs a confirmation (it must be re-downloaded).

---

## 13. Settings (historical proposal)

A dialog/page with a left list (icons + labels, 200 px) and a content pane. Group as:

| Section | Controls |
|---|---|
| **General** | Start with Windows · Start minimised to tray · Closing the window: *Minimise to tray / Quit* · Language of the UI (later) |
| **Voice & audio** | Default voice · Default output device · Default speed · Master volume · **Test sound** |
| **Overlay** | **Hotkey** capture field (shows keycaps; validates registration; shows *"Ctrl+Alt+T is already used by another app"* + suggestions) · Size (S/M/L) · Opacity (85–100 %) · After speaking (hide when done / immediately / stay) · Keep text after speaking · Hide when it loses focus · Reset position |
| **Appearance** | Theme (System / Dark / Light) · Composer text size (16–40 with live preview) · Font (Inter / Atkinson Hyperlegible / System) · Reduce motion (follows Windows) |
| **Accessibility** | High-contrast follow · Announce status to screen reader · Bigger click targets (48 px) |
| **About** | Version, "Offline · No telemetry" statement, licences (link `THIRD_PARTY_NOTICES.md`), Piper credit, GitHub link |

Each setting has a one-line description in `TextSecondary`. Changes apply instantly (no OK/Apply), with a "Reset to defaults" at the bottom of each section.

---

## 14. First run, empty states and errors (historical proposal)

### 14.1 First-run (3 steps, skippable, one dialog)
1. **Welcome** — logo, *"Type it. Say it."*, and the trust line: *"Runs entirely on your PC. Nothing is uploaded."*
2. **Pick a voice** — 2 recommended voices with ▶ preview and **Download** (progress inline). Default to Ryan · Medium (or whichever you ship as default). "Choose later" is allowed if voices were skipped in the installer (`-SkipVoices`).
3. **Try it** — a live composer: *"Press **Ctrl+Alt+T** anywhere, type, press **Enter**."* + a **Test sound** button and output-device picker. Show the borderless-window tip for games here.

Optional: offer **starter phrase packs** (Everyday · Gaming · Work) with toggles, so Saved is never empty.

### 14.2 Empty states

| Place | Illustration / icon | Copy | Action |
|---|---|---|---|
| Saved (empty) | Bookmark icon 24 px | *"Save the things you say often."* / *"Type something, then press ☆ or Ctrl+S."* | **Add starter phrases** (ghost) |
| Recent (empty) | Clock icon | *"Messages you speak will appear here."* | — |
| No voices installed | Person-voice icon, accent | *"You need a voice to start speaking."* | **Open Voices** (primary) |
| No output device | Speaker-off icon | *"No audio output found."* | **Open Windows sound settings** |

### 14.3 Errors and warnings (always: what happened · what to do · one button)

| Situation | Message | Button |
|---|---|---|
| Piper runtime missing | *"The speech engine isn't installed."* | **Repair** (re-run installer step) |
| Voice files missing/corrupt | *"Ryan · Medium needs to be downloaded again."* | **Download** |
| Output device unplugged | *"Wave Link isn't available. Using Default output for now."* (toast, non-blocking) | **Choose device** |
| Hotkey already used | *"Ctrl+Alt+T is used by another app."* | **Choose another** |
| Download failed | *"Couldn't download the voice. Check your connection."* | **Retry** |
| Synthesis failed | *"That message couldn't be spoken."* | **Try again** |
| Empty message | Do nothing; shake composer 2 px once (or a subtle ring flash) | — |

Errors appear **inline in the status pill and as a toast**, never as raw exception text or modal message boxes.

---

## 15. System tray

Menu (top → bottom): **Show FlickVox** · **Show overlay** `Ctrl+Alt+T` · **Stop speaking** (enabled only while speaking) · separator · **Voice ▸** (installed voices) · **Output device ▸** · **Speed ▸** (0.75× / 1× / 1.25× / 1.5×) · separator · **Settings…** · **Quit**.

- Left-click toggles the main window; double-click also brings it to front.
- Tray icon: monochrome bars glyph (light/dark variants that follow the taskbar theme); **speaking** = mint-filled variant. Tooltip: *"FlickVox — Ready"*.
- Keep menu items ≤ 8 at the top level; use native-looking dark menu styling (radius 10, 32 px rows).

---

## 16. Motion, feedback and perceived performance

### 16.1 Motion tokens

| Motion | Duration | Easing | Notes |
|---|---|---|---|
| Overlay in | 120 ms | Cubic ease-out | Fade + scale 0.97 → 1 |
| Overlay out | 100 ms | Cubic ease-in | Fade only |
| Button press | 80 ms | Ease-out | Scale 0.98 |
| Hover colour | 120 ms | Linear | Background only |
| Flyout open | 140 ms | Cubic ease-out | Fade + 8 px slide |
| Speak → Stop morph | 160 ms | Cubic ease-in-out | Cross-fade icon + label, no size change |
| "Preparing" dot pulse | 1.2 s loop | Sine | Opacity 0.5 ↔ 1 |
| Waveform | ~30 fps | — | Driven by real audio level |
| Toast in / out | 180 / 140 ms | Ease-out / ease-in | Slide 12 px + fade |
| List item add | 160 ms | Ease-out | Height + fade |

Guidelines: animate **opacity and transforms**, avoid animating layout/size in hot paths; nothing over 250 ms; no decorative looping animation except the *speaking* waveform.
**Reduced motion:** when `SystemParameters.ClientAreaAnimation` is `false`, replace movement with instant changes or opacity-only fades.

### 16.2 Perceived-performance rules (engineering that *is* UX)
1. **Acknowledge instantly.** On Enter, switch state to *Preparing* and add the message to Recent immediately (before Piper returns).
2. **Cache the last synthesized WAV** so **Repeat** plays instantly; delete it on the next synthesis or app exit (still satisfies your temp-file cleanup).
3. **Warm up** Piper once on app start (tiny throwaway utterance) so the first real message isn't the slowest.
4. **Sentence chunking** for long text: synthesize sentence 1 → start playing → synthesize the rest in the background (README lists streaming as not implemented in 0.1.0 — this is the highest-impact UX upgrade after the visual polish).
5. **Never block the UI thread** on Piper/download; all long work shows progress or a spinner within 100 ms.
6. **Keep the overlay window alive but hidden** (don't construct it on each hotkey) so summon is near-instant.

---

## 17. Microcopy

Tone: plain, short, kind. Sentence case. Verbs on buttons. No exclamation marks.

| Element | Copy |
|---|---|
| Composer placeholder | *Type something to say…* |
| Overlay placeholder | *Type to speak…* |
| Primary button | **Speak** ↔ **Stop** |
| Repeat button | **Repeat** (tooltip: *Speak the last message again · Ctrl+R*) |
| Overlay button tooltip | *Overlay · Ctrl+Alt+T* |
| Status | *Ready · Preparing… · Speaking · Voice not ready* |
| Clear | tooltip *Clear · Ctrl+L* |
| Save phrase | tooltip *Save as phrase · Ctrl+S* → toast *"Saved to phrases."* + **Undo** |
| Delete phrase | toast *"Phrase deleted."* + **Undo** |
| Footer / help | *Enter to speak · Shift+Enter for new line · Esc to stop* (separated, not concatenated) |
| Offline trust | *Runs on your PC. Nothing is uploaded.* |
| Game tip | *To show the overlay over a game, run the game in borderless / windowed fullscreen.* |

---

## 18. WPF implementation notes

> These are practical starting points for .NET 10 WPF. Verify each on your target Windows 10/11 builds.

### 18.1 Suggested structure
```
src/FlickVox/
├─ Themes/     Dark.xaml  Light.xaml  Metrics.xaml  Typography.xaml  Icons.xaml
├─ Styles/     Buttons.xaml  Inputs.xaml  Chips.xaml  Lists.xaml  Flyouts.xaml  Toggle.xaml  Slider.xaml
├─ Assets/     Fonts/ (Inter-*.ttf, Sora-SemiBold.ttf, AtkinsonHyperlegible-*.ttf)  Icons/  FlickVox.ico
├─ Controls/   Keycap  StatusPill  Waveform  Chip  Toast
└─ Views/      MainWindow (quick + expanded)  VoiceManagerDialog  SettingsWindow
```
`App.xaml` merges **`Themes/Dark.xaml` at index 0** (swappable), then Metrics, Typography, Icons, and the Styles.
Use **`DynamicResource`** for every brush so the theme can switch at runtime.

**Tokens (`Themes/Dark.xaml`, excerpt)**
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="Brush.Canvas"          Color="#0F1218"/>
  <SolidColorBrush x:Key="Brush.Surface"         Color="#161A22"/>
  <SolidColorBrush x:Key="Brush.SurfaceRaised"   Color="#1D222C"/>
  <SolidColorBrush x:Key="Brush.SurfaceHover"    Color="#252B37"/>
  <SolidColorBrush x:Key="Brush.StrokeSubtle"    Color="#262D39"/>
  <SolidColorBrush x:Key="Brush.StrokeControl"   Color="#65718A"/>
  <SolidColorBrush x:Key="Brush.Text"            Color="#EEF1F6"/>
  <SolidColorBrush x:Key="Brush.TextSecondary"   Color="#A9B2C1"/>
  <SolidColorBrush x:Key="Brush.TextTertiary"    Color="#8592A5"/>
  <SolidColorBrush x:Key="Brush.Accent"          Color="#4F46E5"/>
  <SolidColorBrush x:Key="Brush.AccentHover"     Color="#5B54F0"/>
  <SolidColorBrush x:Key="Brush.AccentPressed"   Color="#4338CA"/>
  <SolidColorBrush x:Key="Brush.AccentText"      Color="#A5ADFF"/>
  <SolidColorBrush x:Key="Brush.AccentSubtle"    Color="#294F46E5"/>
  <SolidColorBrush x:Key="Brush.Signal"          Color="#3DDBB5"/>
  <SolidColorBrush x:Key="Brush.SignalSubtle"    Color="#263DDBB5"/>
  <SolidColorBrush x:Key="Brush.Danger"          Color="#FF7A7A"/>
  <SolidColorBrush x:Key="Brush.Warning"         Color="#F5B94C"/>
  <SolidColorBrush x:Key="Brush.OverlayBg"       Color="#F012161C"/>
  <SolidColorBrush x:Key="Brush.Focus"           Color="#A5ADFF"/>
</ResourceDictionary>
```
`Themes/Light.xaml` uses the **same keys** with the light values from §4.3.

**Metrics (`Themes/Metrics.xaml`, excerpt)**
```xml
<CornerRadius x:Key="Radius.XS">6</CornerRadius>
<CornerRadius x:Key="Radius.S">10</CornerRadius>
<CornerRadius x:Key="Radius.M">14</CornerRadius>
<CornerRadius x:Key="Radius.L">18</CornerRadius>
```

**Runtime theme switch**
```csharp
public static void ApplyTheme(bool dark)
{
    var uri = new Uri($"Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative);
    Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
}
```
Follow Windows: read `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` and listen to `SystemEvents.UserPreferenceChanged`.

> .NET 9+ ships a built-in **Fluent theme** for WPF (`ThemeMode="System"`, flagged experimental at the time of writing — check your SDK). It gives you a Windows 11 look and backdrop for free, but your own token dictionaries should still win for brand colours. Your README chose "original Fluent-inspired styling", so the token approach above keeps you framework-free.

### 18.2 Dark title bar (and optional Mica)
```csharp
static class WindowBackdrop
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_SYSTEMBACKDROP_TYPE    = 38;  // Windows 11 22H2+ (build 22621)
    const int DWMSBT_MAINWINDOW            = 2;   // Mica

    public static void Apply(Window w, bool dark)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(w).EnsureHandle();
        int useDark = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

        if (Environment.OSVersion.Version.Build >= 22621)
        {
            int mica = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));
        }
    }
}
// Call from Window.SourceInitialized. For Mica to show through, the window/root Background must be
// transparent and the frame extended into the client area (e.g. WindowChrome.GlassFrameThickness = -1).
```
Pragmatic order: **(1)** dark title bar + solid `Canvas` background → 90 % of the visual win; **(2)** custom title bar merged into the header via `WindowChrome` (mark your buttons `WindowChrome.IsHitTestVisibleInChrome="True"`); **(3)** Mica as a finishing touch, with solid `Canvas` as the Windows 10 fallback.

### 18.3 Why text is dark-on-dark, and the fix
Default Windows control styles set their own `Foreground`, which **overrides inheritance** from the window. So the window can be dark while ComboBox/CheckBox/etc. stay black. Set the root and add implicit styles:

```xml
<Window ...
        Background="{DynamicResource Brush.Canvas}"
        TextElement.Foreground="{DynamicResource Brush.Text}"
        FontFamily="{StaticResource Font.UI}" FontSize="14"
        UseLayoutRounding="True" SnapsToDevicePixels="True"
        TextOptions.TextFormattingMode="Ideal">
```
```xml
<Style TargetType="TextBlock"> <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/> </Style>
<Style TargetType="CheckBox">  <Setter Property="Foreground" Value="{DynamicResource Brush.TextSecondary}"/> </Style>
<Style TargetType="ComboBox">  <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/> </Style>
<Style TargetType="ComboBoxItem"> <Setter Property="Foreground" Value="{DynamicResource Brush.Text}"/> </Style>
```
(Long-term, replace the ComboBoxes with the chip + flyout pattern from §9.3; you'll fully own the template.)

### 18.4 Fix the `VoiceDefinition { … }` text
Bind to something human-readable instead of the record's default `ToString()`:
```xml
<ComboBox ItemsSource="{Binding Voices}" SelectedItem="{Binding SelectedVoice}"
          DisplayMemberPath="DisplayName"/>
```
and derive a friendly label from the id (`en_US-ryan-medium` → `Ryan · Medium`):
```csharp
public static string FriendlyName(string id)
{
    var p = id.Split('-');                       // [locale, name, quality]
    return p.Length == 3 ? $"{Cap(p[1])} · {Cap(p[2])}" : id;
    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
```

### 18.5 Primary button template (pattern for all buttons)
```xml
<Style x:Key="Button.Primary" TargetType="Button">
  <Setter Property="Foreground" Value="White"/>
  <Setter Property="Background" Value="{DynamicResource Brush.Accent}"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Height" Value="44"/>
  <Setter Property="Padding" Value="20,0"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Grid>
          <Border x:Name="FocusRing" Margin="-3" CornerRadius="13" BorderThickness="2" BorderBrush="Transparent"/>
          <Border x:Name="Bd" CornerRadius="10" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </Border>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Brush.AccentHover}"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Brush.AccentPressed}"/>
          </Trigger>
          <Trigger Property="IsKeyboardFocused" Value="True">
            <Setter TargetName="FocusRing" Property="BorderBrush" Value="{DynamicResource Brush.Focus}"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Bd" Property="Opacity" Value="0.4"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

### 18.6 Unified window skeleton
```xml
<Window x:Class="FlickVox.MainWindow"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" Width="410" Height="300">
  <Border CornerRadius="14" Background="{DynamicResource Brush.Canvas}"
          BorderBrush="{DynamicResource Brush.StrokeControl}" BorderThickness="1">
    <!-- one composer; quick mode hides expanded rows and uses 360 × 48 -->
  </Border>
</Window>
```
- Preserve the shared input when changing dimensions.
- Clamp restored coordinates to a visible monitor working area. Preserve migrated personal dimensions.
- Do not draw an unverified waveform.

### 18.7 Rendering, DPI, accessibility plumbing
- **Per-monitor DPI v2** in `app.manifest` so text stays sharp when moving between monitors or at 125/150 %:
  ```xml
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  ```
- Status text: `AutomationProperties.LiveSetting="Polite"` so Narrator announces *Speaking / Ready / errors*.
- Every icon button: `ToolTip` + `AutomationProperties.Name`.
- High contrast: when `SystemParameters.HighContrast` is true, swap to a dictionary that maps tokens to `SystemColors` brushes.
- Use `Keyboard.Focus`/`FocusManager.FocusedElement` to autofocus the composer on activation.

---

## 19. Roadmap (in the order I'd do it)

### Phase 0 — "Stop looking unfinished" (1 evening)
- [ ] Root `Background`, `TextElement.Foreground`, and implicit control styles (§18.3)
- [ ] Dark title bar via DWM (§18.2 step 1)
- [ ] Voice combo shows `Ryan · Medium` (§18.4)
- [ ] Remove the permanent footer; content in a `ScrollViewer` (narrow window no longer clipped)
- [ ] Remove the stray hairline / blank band above the header
- [ ] Compact status text legible; long errors in a separate notice
- **Done when:** both screenshots have consistent dark theme, no truncated/clipped text, all text ≥ 4.5 : 1.

### Phase 1 — Design system (1 weekend)
- [ ] `Dark.xaml` / `Light.xaml` / `Metrics.xaml` / `Typography.xaml`
- [ ] Bundle Inter (+ Sora for wordmark); apply type scale
- [ ] Fluent icon set as XAML geometries; replace `✕` and `↗`
- [ ] Styles: Button (primary/secondary/ghost/icon), TextBox, CheckBox → Toggle, Slider, ScrollBar, ToolTip, Keycap, StatusPill
- **Done when:** no control shows a default Windows look; changing one token recolours the app.

### Phase 2 — Unified compact window
- [ ] One shared composer and Send ↔ Stop action across quick and expanded modes
- [ ] Saved phrase chips with local Alt+1…9, quiet Voice/Output/Speed footer, Recent popover
- [ ] Position/state migration and 360 × 48 quick-pill sizing
- **Done when:** expansion does not lose the draft and both modes remain usable at common text scales.

### Phase 3 — Quick-pill interaction
- [ ] Dedicated drag grip, visible Send/Stop, monitor-aware position and immediate focus
- [ ] Idle · Typing · Preparing · Speaking · Error; green only during playback
- [ ] Enter/Shift+Enter/Esc and hide only after successful playback
- **Done when:** the hotkey opens the existing window in quick mode without a second instance.

### Phase 4 — Supporting surfaces (1–2 weekends)
- [ ] Voice Manager redesign (§12)
- [ ] Settings (§13) incl. hotkey capture with conflict detection
- [ ] Tray menu (§15)
- [ ] First-run flow + empty/error states (§14)
- [ ] Toasts with Undo

### Phase 5 — Brand & release
- [ ] Logo, multi-size `.ico`, tray glyphs
- [ ] README screenshots/GIFs (§22), repo description + topics
- [ ] Installer polish, code signing (README already flags Smart App Control)
- [ ] Accessibility Insights pass + Narrator smoke test (§20)

### Later / nice-to-have
Phrase categories · import/export phrases · per-app output routing presets · pronunciation dictionary ("say *Kubernetes* like…") · profiles (Gaming / Work) · sentence streaming · UI localisation.

---

## 20. Accessibility checklist
- [ ] All text ≥ 4.5 : 1; icons and control borders ≥ 3 : 1 (tokens in §4.4 already comply)
- [ ] Every function reachable and operable by **keyboard only**; logical Tab order; no keyboard traps
- [ ] Visible **focus ring** (2 px `AccentText`) on every interactive element
- [ ] Click/touch targets **≥ 36 px** in dense areas, primary actions **≥ 44 px** (WCAG 2.2 minimum is 24 px; aim higher for motor-impaired users)
- [ ] `AutomationProperties.Name` on all icon-only buttons; status changes announced via live region
- [ ] Works at **200 % DPI** and with a **user-set composer size up to 40 px** without clipping
- [ ] Follows **Windows High Contrast** themes
- [ ] Honours **reduced motion**
- [ ] Meaning never conveyed by colour alone (state = colour + icon + text)
- [ ] Optional **Atkinson Hyperlegible** composer font and "bigger targets" mode
- [ ] Test with **Narrator** and **Accessibility Insights for Windows**

## 21. Visual QA checklist (before every release)
- [ ] Dark, Light and High-contrast themes
- [ ] 100 / 125 / 150 / 200 % scaling; mixed-DPI dual monitors
- [ ] Windows 10 (no Mica, no Segoe UI Variable) **and** Windows 11
- [ ] Window sizes: compact default/minimum, resized wider, and Windows text scaling
- [ ] Long voice/device names truncate with ellipsis + tooltip
- [ ] Empty, loading, error, speaking states for every screen
- [ ] Overlay on primary and secondary monitors, over a borderless-windowed game, over a light-coloured app (legibility of the dark pill)
- [ ] Focus returns to the previous app after the overlay hides
- [ ] Pixel check: 1 px strokes are crisp (`UseLayoutRounding`, `SnapsToDevicePixels`)
- [ ] No debug/`ToString()` output anywhere in the UI

## 22. README & portfolio polish
Your repo currently shows *"No description, website, or topics"* and *"Screenshots will be added later."* Fixing this makes the project look shipped.
- **Repo description:** *"Offline, lightning-fast text-to-speech overlay for Windows — powered by Piper."*
- **Topics:** `text-to-speech` `piper-tts` `wpf` `dotnet` `windows` `accessibility` `aac` `overlay` `naudio` `offline`
- **Hero GIF (8–10 s):** hotkey → type → Enter → truthful speaking state → overlay hides after playback. Then 3 stills: main window (dark), quick/expanded overlay, Voice Manager.
- **Badges:** .NET 10 · Windows 10/11 · Offline · licence.
- **Sections:** *Why FlickVox* (3 bullets: fast, offline, works with Wave Link/voice chat) · Screenshots · Install · Shortcuts table · Roadmap · Credits (Piper, NAudio).
- Add a **social-preview image** (1280 × 640): logo tile + tagline on the ink background.

## 23. Tools & references
- **Design:** Figma (build the tokens as variables; prototype the overlay states), Fluent 2 design language (for Windows conventions), Fluent UI System Icons, Inter, Sora, Atkinson Hyperlegible.
- **Contrast/accessibility:** WebAIM contrast checker, Accessibility Insights for Windows, Narrator.
- **WPF debugging:** Visual Studio *Live Visual Tree* / *Live Property Explorer* (great for finding that stray hairline), Snoop.
- **Study for feel (not to copy):** Windows 11 Settings (native fit), PowerToys Command Palette / Raycast (summon-and-dismiss overlays), Linear (restraint and density), AAC apps such as Proloquo and The Grid (phrase-first interaction).

---

## 24. Cheat sheet

**Do**
- Make the composer and the overlay the two best things in the app
- Use mint only for "audio is live"; accent only for the single primary action
- Keep every screen usable with the keyboard alone
- Show state within 100 ms, every time
- Hide rare settings; show current values as chips

**Don't**
- Don't mix light and dark surfaces
- Don't show developer strings (`VoiceDefinition { … }`)
- Don't put four equal-weight buttons in a row
- Don't use text glyphs as icons
- Don't animate for decoration, or block the UI thread

---
*End of specification. Suggested next step: implement Phase 0 today, then build a Figma or XAML prototype of the overlay (the highest-leverage screen) before touching the rest.*
