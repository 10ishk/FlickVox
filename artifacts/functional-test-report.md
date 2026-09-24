# FlickVox functional audit — 2026-09-24

This is a development-build audit, not a release certification. `PASS` means the path was exercised; `CODE-REVIEWED` means its event wiring/logic was inspected but not exercised; `BLOCKED` means the available environment or stopped desktop automation prevented verification.

## A. Environment

- SDK: existing .NET 10.0.401 at `%LOCALAPPDATA%\QuickSpeak-build-sdk`; no SDK installation.
- Development executable: self-contained `win-x64` publish under `src/FlickVox/bin/Release/publish`. Final Release build: 0 warnings, 0 errors. One process launched from that directory.
- Piper: official 2023.11.14-2 Windows runtime under `%LOCALAPPDATA%\FlickVox\runtime\piper`, separate from app binaries. No Piper process remained after the regression runs.
- Voices: all six official ONNX/config pairs under `%LOCALAPPDATA%\FlickVox\runtime\voices`; lengths and MD5 digests matched the official `rhasspy/piper-voices` manifest before testing. No models were removed or downloaded again during the continuation.
- Audio: the development machine exposes multiple WinMM output endpoints, including a virtual audio endpoint. The tests can confirm NAudio completion/error handling, but cannot prove sound reached speakers or an external mixer.
- User data: original and pre-final settings/phrase snapshots remain in `%TEMP%\FlickVox-functional-audit-backup`. No backup was blindly restored.

## B. Test matrix

| Feature | Test/observation | Result | Evidence / limitation |
|---|---|---|---|
| Startup/deployment | Launched final development publish | PASS | One FlickVox process from expected publish directory; required app/NAudio files present. |
| Single instance | Started a second development process | PASS | Second process exited; process count stayed one. |
| Piper runtime | Executed the official runtime directly | PASS | Valid RIFF/WAVE output from all six models. |
| End-to-end software path | Ran `PiperSpeechService` with default output | PASS | `Generating,Speaking,Ready`; playback task completed. This is not proof of audible speaker output. |
| Completed WAV cleanup | Compared GUID WAV files before/after playback | PASS | Zero new files remained after service returned. Previously observed pre-fix WAV leaks remain outside this test. |
| Generation cancellation | Waited until child Piper PID existed, then stopped | PASS | Cancellation propagated; zero new WAVs and zero orphaned child PIDs. |
| Playback cancellation | Waited for `Speaking`, then stopped | PASS | Cancellation propagated; zero new WAVs and zero orphaned child PIDs. |
| Rapid recovery | Started/aborted long speech, immediately started short speech | PASS | First cancelled; second reached `Ready`; no overlap residue. |
| Invalid audio device | Requested unavailable WinMM device | PASS | `MmException` surfaced and source WAV handle was released. |
| Main composer/actions | Prior desktop pass: typing, Speak/Stop state, Save/Clear visibility, Repeat enabling | PASS / BLOCKED | Normal Speak and Save were exercised; full keyboard and rapid UI interactions remain manual. |
| Voice flyout | Prior desktop pass: six choices, family switch, chip update | PASS | Ryan Medium → Lessac Medium without restart; app-level speech succeeded for both. |
| Speed flyout | Prior desktop pass: slider and value/persistence changed; Escape dismissed | PASS | Actual UI value changed; Piper length-scale mapping is code-reviewed. |
| Output flyout | Inspected event wiring and selected-device index mapping | CODE-REVIEWED | Main flyout selection and secondary-device playback not exercised. Settings output list was observed. |
| Saved phrases | Prior desktop pass: saved a disposable phrase, list updated, file written | PASS | The sole audit phrase was moved recoverably to backup before final restart. Post-restart visual list check remains manual. |
| Recent messages | Prior desktop pass: Recent segment displayed submitted messages | PASS / CODE-REVIEWED | Duplicate ordering and behavior after restart are code-reviewed, not exercised. Persistent history is not implemented. |
| Overlay | Prior desktop view showed compact overlay and Typing state; hide timing/coordinate logic reviewed | PASS / BLOCKED | Header/hotkey attribution, drag, focus, all states and hide-after completion need manual UI testing after the fixes. |
| Voice Manager | Prior desktop pass: Ryan/Lessac groups, six installed statuses, Use selection, selected voice protected | PASS / BLOCKED | Download/cancel/remove not exercised because all six verified personal models were preserved. Displayed size metadata was corrected. |
| Settings/themes | Prior desktop pass: General toggle saved; Light theme applied to Settings and Main; font and size changed; audio devices enumerated | PASS / BLOCKED | Remaining settings, follow-system changes, restart persistence and High Contrast not fully exercised. |
| Global hotkey | Registration and event wiring inspected | CODE-REVIEWED | Exact invocation/result not conclusively isolated before desktop automation stopped. |
| System tray | Creation/menu/Exit handlers inspected; single-instance exercised | CODE-REVIEWED | Tray interactions not directly exercised. Exit-vs-hide conflict was corrected in code. |
| Error recovery | Invalid device tested; missing voice/runtime paths inspected | PASS / BLOCKED | Missing/corrupt voice, interrupted download and hotkey conflict need isolated/manual tests. |
| Installer/uninstaller | Read-only path and scope review; no execution | CODE-REVIEWED | Installer publish path corrected. Neither script was run. |

## C. Six-voice speech results

All six models/configs passed official-manifest size and MD5 checks. Direct Piper tests produced RIFF/WAVE files with nonzero PCM data and exit code 0. A longer multiline Ryan Medium utterance also completed. Only the two indicated voices were exercised through FlickVox itself before desktop automation stopped.

| Voice | Model/config | Direct Piper | In-app generation / NAudio completion | Audible output |
|---|---|---|---|---|
| Ryan Low | PASS | PASS | BLOCKED | BLOCKED |
| Ryan Medium (default) | PASS | PASS | PASS | BLOCKED |
| Ryan High | PASS | PASS | BLOCKED | BLOCKED |
| Lessac Low | PASS | PASS | BLOCKED | BLOCKED |
| Lessac Medium | PASS | PASS | PASS | BLOCKED |
| Lessac High | PASS | PASS | BLOCKED | BLOCKED |

## D. Defects and repairs

1. **Observed:** completed speech left GUID temporary WAVs. Cause: NAudio's reader/output outlived `PlayAsync`, so Piper's `finally` could not delete the file. Fix: playback now disposes the output and reader before returning, including cancellation/error paths. Retest: zero new WAVs after normal playback, playback cancellation, generation cancellation, and rapid recovery.
2. **Code-confirmed:** cancellation of `WaitForExitAsync` did not explicitly terminate the child Piper process. Fix: cancel registration kills the relevant child process tree, waits for exit, then removes its WAV. Retest: observed a live child PID before cancellation; no orphan after.
3. **Code-confirmed:** rapid requests could overlap service work and stale callbacks could reset the main status. Fix: serialize speech runs and version main-window status updates. Retest: service rapid Stop/Speak passed; rapid UI state remains manual.
4. **Code-confirmed:** overlay hide-after-speaking hid immediately on Enter, suppressing the Speaking/error display. Fix: hide only after successful playback; retain the entered text on error. Overlay UI retest remains manual.
5. **Code-confirmed:** a saved overlay position could leave most of the pill outside the working area. Fix: clamp restored coordinates to the selected monitor's working area. Multi-monitor/DPI UI verification remains manual.
6. **Observed:** Voice Manager's approximate model sizes were materially wrong. Fix: corrected six displayed sizes against the official voice manifest. Final UI display check remains manual.
7. **Code-confirmed:** installer expected an obsolete nested publish path. Fix: use the actual development publish path. Installer was not run.
8. **Code-confirmed:** tray Exit could be converted into hide-on-close. Fix: explicit exit flag allows real application shutdown and cleanup. Tray UI retest remains manual.

## E. Unverified / manual checks

Desktop Computer Use was stopped by the user, so no further desktop actions were attempted. In the final development instance, manually check:

1. Enter, Shift+Enter, Esc, Ctrl+L, Ctrl+S, Ctrl+R, Speak/Stop/Repeat/Preview, empty/whitespace messages, and Ready/error recovery.
2. Each Voice/Output/Speed flyout, selected values, dismissal, long device-name tooltip, and playback through a chosen secondary endpoint. Listen or capture output to establish audibility and Wave Link routing.
3. Overlay via header and Ctrl+Alt+T; input focus, Enter/Shift+Enter/Esc, drag versus text selection, position restore, hide only *after* speech, stay-open mode, and offscreen/multi-monitor recovery.
4. Voice Manager download/progress/cancel/interrupted download and removal confirmation **only with disposable test resources**; do not delete the six verified models.
5. Settings reopen/restart persistence, Dark/Light/System across open windows, tray open/overlay/stop/repeat/settings/exit, hotkey conflict, and High Contrast if the user elects to enable it.

## F. Not implemented (distinct from blocked tests)

The design specification still describes features absent from this implementation: persistent recent-history behavior despite an unused preference field; pinned overlay phrase chips/number shortcuts; genuine audio-level waveform; richer tray voice/output/speed submenus; several advanced Settings options (hotkey capture, overlay opacity/after-speaking choices, start-with-Windows); and phrase editing/deletion. These were not reported as passed.

## G. Release blockers and data reconciliation

- Public distribution is not verified: audible output, all six voices through the app, important overlay/tray UI paths, and download/removal recovery need manual confirmation. Do not publish a release based on this audit.
- The original settings backup and a newer pre-final snapshot differ in theme, voice, overlay position, hide-after, composer size/font (and at one point speed). The current settings were changed after the backup. **The current settings were preserved**, and both snapshots remain for user choice.
- The only phrase file entry was the audit-created disposable phrase. After verifying its ID and count, it was moved to `audit-only-phrases.json` in the backup directory before the final launch; it can be recovered. No personal phrase was deleted.
- Historical pre-fix temporary WAV files were left untouched because ownership of every file could not be established safely. The fixed service created no new leftovers in regression tests.
