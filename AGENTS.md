# mpv-winui-player

WinUI 3 player embedding libmpv. Three parts: the C# WinUI app
(`mpv-winui/mpv-winui/`), the C++/WinRT component (`mpv-winui/mpv-winrt/`),
and the config layer (`mpv-winui-lazy/`). User-facing docs live in
`README.md`; this file is for agents changing the code.

## Work loop

1. **Build** — run `.\build.ps1 -Configuration Release -Platform x64` from the repo root (VS MSBuild for the C++ component, then `dotnet build` for the app). Use full parameter names: `-Release x64` silently falls back to Debug.
   Done when: exit code 0, no error lines, and the exe exists at `mpv-winui\mpv-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\mpv-winui.exe`.
2. **Run** — launch the exe with a test file and wait for the window.
3. **Test the change** — drive the app through the mpv IPC pipe (`NamedPipeClientStream('.', 'mpvpipe')`) and verify by log output or screenshot.
4. **Finish** — remove debug instrumentation (grep `\[DBG-`), stage only repo files (never `bin/`, `obj/`, `dist/`, `libs/`), commit and push to `github main`.

## Reference

### Test facts

- IPC: `NamedPipeClientStream('.', 'mpvpipe')`; send JSON `{"command":[...],"request_id":N}`, read lines until the matching request_id.
- Logs: `%LOCALAPPDATA%\mpv-winui\logs\mpv-winui.<date>.log.txt`, plus `display-info.log`.
- Config runs from `%LOCALAPPDATA%\mpv-winui\mpv` (synced by ConfigDeployer at startup).
- Settings are in registry `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app`; booleans stored as `"true"` / `"false"`.
- Defaults live in `AppSettings.cs`; live mpv mapping is `MpvSettings.ToCommand`.
- PrintWindow misses WinUI popups; use CopyFromScreen for visual verification.
- UIA BoundingRectangle has a ×1.5 DPI scale factor on this machine.

### Architecture gotchas

- **Mouse input**: libmpv never sees Win32 mouse input in composition mode. The app forwards it via WH_KEYBOARD hook + window subclass in MpvPlayerPage_Input/MpvPlayerPage_Mouse.
- **Command strings**: `mpv_command_string` parses C-style escapes. Windows paths need doubled backslashes inside quotes (`Q()` in `MpvSettings.cs`).
- **Command ordering**: settings ApplyAll, menu commands and `OpenAsync` run through `MpvCommandQueue` (FIFO worker); input forwarding stays direct.
- **Position UI**: event-driven; `time-pos` observed natively + 100ms coalescing timer — never reintroduce polling.
- **Fullscreen**: driven by mpv's `fullscreen` property (`HandleFullscreenProperty`). State-based, never toggle-based. Always call `RefreshAdaptiveState()` after presenter transitions.
- **PiP**: dedicated borderless always-on-top window reusing `PlayerControl`. ExitPiP defers sizing until main window is shown and laid out (ActualWidth=0 bug).
- **Overlay bar**: activity-based visibility with 1.5s idle timer. ControlPanelGradient Height=120 only in overlay mode.
- **Control-bar layout**: `ApplyBarOrders` rebuilds three CommandBars from canonical lists on every apply — never filter through current items.
- **Playback modes**: single mode button cycles 不循环→顺序→单曲循环→随机. RepeatButton hidden; ShuffleButton reused. Glyphs F175/F172/EF34/EF37.

### Settings architecture

- `SettingsPage.Options.cs`: tree builder (BuildSettings) with sectionMap + sectionOrder + optionOrder dictionaries. Missing sectionMap entry throws at startup (fail-loud).
- Category partials: `Options.Playback.cs`, `Options.Video.cs`, etc.
- `Controls/Option*.xaml/cs`: per-type renderers (Boolean, StringList, Integer, Double, Color, CheckList, Layout, ShaderList).
- Dependency gating: `RefreshWarningsAndEnabled()` evaluates Warning/IsEnabled/IsVisible rules.
- `tools/check-settings-drift.py`: validates AppSettings ↔ ToCommand ↔ option keys consistency.
- `tools/check-localization.py`: validates AppLang properties × 8 language JSON files.
- App-level settings (no ToCommand mapping) must be whitelisted in check-settings-drift.py.

### Right-click menu

- Built from dyn_menu.lua's `menu-data` property via `BuildMenuFlyoutFromData()`.
- Items appear in input.conf annotation order (same as mpv-menu-plugin).
- No icons, no filtering, no renames — plain text rendering.
- `HiddenMenuTitles` was removed; to hide entries edit input.conf directly.
- A native C++ builder (`MpvMenuBuilder.cpp`) exists but is disabled pending memory-management review.

### Thumbnail preview (seek-bar)

- In-process second libmpv instance (`MpvPreviewer.cpp`) with software rendering.
- Thumbfast-style: async keyframe seeks gated on `seeking` flag; demuxer cache (32MiB fwd/16MiB back); FILE_LOADED deferred seek.
- C# warms up on MediaOpened; min position delta 0.25s.
- Adjustable: size (180/248/320px) and update interval (40–600ms) in settings.

### License guardrails

- App code: LGPL-2.1. Project-written config-layer files: LGPL-2.1-or-later.
- dyn_menu.lua / dialog.lua are GPL-2.0-only source (still loaded but native replacement exists).
- Update `mpv-winui-lazy/THIRD_PARTY_NOTICES.md` when adding third-party components.

## Pointers

- Editing menus / input.conf: read `docs/localization.md` first.
- Localization: all user-facing strings go through AppLang × 8 Languages/*.json. Run `python tools/check-localization.py` after changes.
- Adding settings options: read `docs/localization.md`, run both checkers after changes.
