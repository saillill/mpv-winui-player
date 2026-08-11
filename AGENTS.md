# AGENTS.md

WinUI 3 player embedding libmpv. Three parts: the C# WinUI app
(`mpv-winui/mpv-winui/`), the C++/WinRT component (`mpv-winui/mpv-winrt/`),
and the config layer (`mpv-winui-lazy/`). User-facing docs live in
`README.md`; this file is for agents changing the code.

## Work loop

1. **Build** — run `.\build.ps1 -Debug x64` from the repo root (VS MSBuild for
   the C++ component, then `dotnet build` for the app).
   Done when: exit code 0, no error lines, and the exe exists at
   `mpv-winui\mpv-winui\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\mpv-winui.exe`.
2. **Run** — launch the exe with a test file and wait for the window.
   Done when: UIA finds a window with that process id within ~5 seconds.
3. **Test the change** — before fixing a bug, build a *tight*, red-capable
   loop (see the diagnosing-bugs skill): one command that fails on the exact
   symptom and passes after the fix. Drive the app through the mpv IPC pipe
   and UIA/Win32 from PowerShell; verify by property change or screenshot,
   not by "it builds".
4. **Finish** — remove debug instrumentation (grep `\[DBG-`), stage only repo
   files (never `bin/`, `obj/`, `dist/`, `libs/`), commit with the hypothesis
   that was correct, and push to `github main`.

## Reference

### Test facts

- `dotnet test mpv-winui\mpv-winrt-test\mpv-winrt-test.csproj -c Debug` runs
  the mpv-winrt tests from the dotnet CLI (the test project references the
  built `.winmd` directly). VS MSBuild is only needed to rebuild `mpv_winrt`
  itself; `build.ps1` does that first.
- IPC: `NamedPipeClientStream('.', 'mpvpipe')`; send JSON
  `{"command":[...],"request_id":N}`, read lines until the matching
  `request_id` (mpv events interleave with responses).
- `mouse_click.ps1` in the session work folder sends **left** clicks only.
  Right-click needs `mouse_event(0x0008/0x0010)` (RIGHTDOWN/UP).
- `PrintWindow` misses WinUI popups (menus, flyouts). Capture menus with a
  full-screen `CopyFromScreen` instead.
- `keep-open=always` pauses at the end of a file. A 6-second test file is
  already paused when you finish setting up; send `set pause no` + `seek 0
  absolute` over IPC first, or use a 30-second file.
- Right-click menu is a WinUI `MenuFlyout` built from mpv `menu-data`; the PiP
  window title is `Picture in picture`.
- Logs: `%LOCALAPPDATA%\mpv-winui\logs\mpv-winui.<date>.log.txt`, plus
  `display-info.log` in the same folder.
- Config runs from `%LOCALAPPDATA%\mpv-winui\mpv` (synced by
  `mpv-winui-lazy\deploy-config.ps1` via robocopy `/MIR`; runtime data like
  `_cache/`, `recent.json`, and logs are excluded).
- Settings are in registry
  `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app`;
  booleans are stored as `"true"` / `"false"`. Defaults live in
  `AppSettings.cs`; the live mpv mapping is `MpvSettings.ToCommand`.

### Architecture gotchas

- **Mouse input**: libmpv never sees Win32 mouse input in composition mode.
  The app forwards it: wheel/left/double-click in `MpvPlayerPage_Mouse.cs`,
  PiP wheel in `PiPWindow.xaml.cs`, X1/X2 via the window subclass in
  `MpvPlayerPage_Display.cs`. `input.conf` is the source of truth for what
  each gesture does.
- **Command strings**: `mpv_command_string` parses C-style escapes. Windows
  paths need doubled backslashes inside quotes (`Q()` in `MpvSettings.cs`);
  a raw `C:\Users\...` silently keeps the old option value.
- **PiP**: a dedicated borderless always-on-top window reusing `PlayerControl`
  in centered compact mode; the main window is hidden and Alt+F4 restores it.
  Left-press on the PiP video drags the window — do not bind left-click pause
  there.
- **HDR**: the app writes `user-data/mpvw/color-kind` and the refresh rate;
  `profiles.conf` switches output. `d3d11-output-csp=display-p3` is invalid —
  keep the `target-*` triplet for HDR.
- **thumbfast**: spawns a standalone `mpv.exe` for previews; keep
  `quit_after_inactivity` non-zero so abrupt app exits do not orphan it.
- **Fullscreen is driven by mpv's `fullscreen` property** (`HandleFullscreenProperty`
  applies the presenter, full-window page state and overlay; the app button
  only sends `set fullscreen yes/no`). Keep it state-based, never toggle-based,
  or ESC ("set fullscreen no") cannot leave fullscreen.
- **Overlay look**: in overlay mode `ControlPanelGrid` is transparent (PiP
  style: only the gradient mask fades the video into the bar) with
  `RequestedTheme=Dark` so glyphs stay white. No solid panel box; on exit
  reset opacity/translate/visibility fully (a mid-fade exit used to leave
  the windowed bar partially expanded).
- **Fullscreen + PiP**: hiding and re-showing a FullScreen-presenter window can
  leave the XAML overlay stale (video renders, control bar does not). On PiP
  exit, cycle the presenter Default → FullScreen when `_isFullScreen`, and do
  not call `ExitPiP` when `_pipWindow` is null (startup `ApplyPiP()` with
  `WindowPiP=false` used to re-attach the swap chain before mpv init and crash).
- **Swap chain sizing**: `UpdatePlayerViewSize` must use `Ceiling`, not
  `Floor`. At fractional DPI (e.g. 175%) logical width × scale lands just
  below the physical size, and flooring leaves a 1px seam on the right edge
  of fullscreen.
- **Overlay bar visibility is activity-based**: any pointer move over the
  video, mask or bar calls `NotifyOverlayPointerActivity()` (show + restart
  the 1.5s idle timer); the idle timer retracts the bar. Only moves >= 3px
  count as activity, otherwise sub-pixel jitter keeps the bar visible for
  many seconds after the mouse stopped. Do not reintroduce position-only
  show/hide — the mask and bar consume moves over themselves.
- **Control-bar shell**: `ControlPanelGradient` is the fullscreen mask shell.
  It must be `Height=120` only in overlay mode and `Auto` (NaN) in windowed
  mode, otherwise a tall transparent strip remains between the video and the
  bar (the "background box" behind the controls).
- **Overlay slide**: the main-window pop-out slide animates the XAML
  `TranslateVertical` (TranslateTransform.Y) with a Storyboard - render
  transforms are layout-independent, so a resize/zoom while the bar is hidden
  cannot strand it above the bottom edge. PiP (a second top-level window)
  uses a 16ms `DispatcherTimer` tween on the same XAML values because a
  Storyboard crashes the compositor there and the composition `Offset`
  animation strands the bar after zoom (same stale-layout class of bug).
  Never write `Visual.Offset` manually (layout owns it), and never use
  `CompositionPropertySet` keys like `Translation.Y` (not animatable in WinUI
  3 desktop; throws in the batch-completed callback).
  When starting a new overlay animation, stop only the in-flight Storyboard;
  do not call `StopPanelAnimations()` (it clears `_panelAnimating`, which
  defeats the same-direction re-entry guard and makes every pointer move
  restart the pop-out animation - the bar twitches).
- **Full-window state**: `PlayerControl_OnFullWindowRequest` tracks
  `_isFullWindow` explicitly — never derive it from `GoToState`'s return value
  (it only reports whether the state changed, which flips the flag when the
  state is already active). After every mode switch call
  `PlayerControl.RefreshAdaptiveState()` so the width-adaptive state cannot
  leave the buttons collapsed.
- **Control-bar layout**: `ApplyBarOrders` clears and rebuilds the three
  CommandBars from canonical lists on every apply — never filter through the
  currently present items, or a partial/PiP pass permanently drops buttons
  (the bar ends up with only the progress bar and times). `ApplyControlBarStyle`
  also restores the PiP-hidden button set on every non-compact apply. Rewind,
  FastForward and Stop were removed entirely (they were only Collapsed by
  design before).
- **Top bar buttons**: the top menu row keeps three icon buttons at the far
  right even when the playlist is collapsed, in order: always-on-top
  (Fluent pin F602 unpinned / F604 pinned, sends `cycle ontop`; glyph driven
  by the mpv `ontop` property), screenshot (Fluent camera F255, sends
  `screenshot`), playlist toggle (Fluent panel-right-contract E8C3, opens and
  closes the sidebar; this is the merged playlist/collapse button) and refresh
  (Fluent arrow-clockwise F13E). When the playlist panel is open the same
  buttons move into `PlaylistToolBar` next to refresh (ontop, screenshot,
  close, refresh); the `ShowPlaylist` / `HidePlaylist` states toggle
  `TopBarButtons.Visibility` accordingly. All glyphs come from the bundled
  `FluentSystemIcons-Regular.ttf` (Microsoft, MIT) so their optical size and
  baseline match. Keep these codepoints with the bundled font when touching
  these buttons.
- **PiP glyphs**: the control-bar PiP toggle and the PiP window's top-left
  restore button use the Fluent PiP enter/exit glyphs (E97E enter PiP /
  E981 exit PiP); the PiP window button always shows the exit glyph.
- **PiP overlay**: the PiP top buttons react only to the top 90px mask and
  the status bar only to the bottom 120px mask; elsewhere both retract.
  The status bar fades via composition opacity key-frames (frame-aligned)
  with a 16ms timer driving only the XAML TranslateTransform slide; the top
  buttons use the timer tween on XAML opacity. No Storyboard in the second
  window and no composition Offset (layout owns it). Hidden buttons are set
  `Visibility=Collapsed` on fade-out so they stop hit-testing and cannot show
  their tooltip.
- **PiP video size**: the swap chain follows the PiP panel through
  `VideoPanel.SizeChanged`/`CompositionScaleChanged` (`UpdatePiPPanelSize`);
  without it the video keeps the main window's surface size after repeated
  toggles and only a corner is visible.
- **Control-bar icon set**: the transport icons use Segoe glyphs except the
  shuffle and PiP buttons, which use Fluent glyphs (`arrow_shuffle` EF37 on /
  EF3D off, PiP E97E/E981) at FontSize 20 so their optical size matches the
  Segoe neighbors (Fluent 24-grid glyphs render smaller at the same size).
- **Track flyout style**: `TrackSelectionButton` keeps the `ED1F` glyph; its
  `FlyoutPresenter` uses `AcrylicInAppFillColorDefaultBrush` +
  `CardStrokeColorDefaultBrush` (Mica/Acrylic look) instead of the translucent
  `MpvControlBackground`.
- **Overlay gradient**: the fullscreen/borderless mask shell is 120px with a
  ~90%-black base stop (mpv-lazy/ModernX style); windowed mode keeps the bar
  backgroundless.
- **Settings layout**: `SettingsPage.xaml.cs` is the page shell (wiring and
  events), `SettingsPage.Options.cs` defines the option tree, the
  `SettingsPage.Options.*.cs` partials hold one category each (Audio, Video,
  Subtitles, Playback, Program, Advanced, Screenshot, PathFolders) and
  `SettingsPage.Actions.cs` holds settings actions. Add new options in the
  matching category partial, not in the page shell.

### License guardrails

- App code: LGPL-2.1. Project-written config-layer files:
  LGPL-2.1-or-later.
- `dyn_menu.lua` / `dialog.lua` are GPL-2.0-only source. No GPL binaries in
  the repo (`menu.dll` was removed); do not re-add binaries without a license
  text.
- mpv-lazy config files are UNLICENSED per upstream; preserve provenance and
  prefer rewriting over copying.
- Update `mpv-winui-lazy/THIRD_PARTY_NOTICES.md` whenever third-party
  components are added.

## Pointers

- Editing menus / `input.conf`: read `docs/menu-audit-20260807.md` first
  (provenance and history of the 153-item menu).
- Touching the config layer: read `mpv-winui-lazy/README.md`.
- Adding components or checking licenses: read
  `mpv-winui-lazy/THIRD_PARTY_NOTICES.md`.
