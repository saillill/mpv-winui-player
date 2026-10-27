# mpv-winui-player

WinUI 3 player embedding libmpv. Three parts: the C# WinUI app
(`mpv-winui/mpv-winui/`), the C++/WinRT component (`mpv-winui/mpv-winrt/`),
and the config layer (`mpv-winui-lazy/`). User-facing docs live in
`README.md`; this file is for agents changing the code.

## Layout

```
mpv-winui/mpv-winui/        C# app
  App.xaml(.cs) Program.cs  entry points — must stay at the project root
  Shell/                    MainWindow.*, AppContext.cs
  Modules/<Area>/           one folder per feature area
  Assets/ Languages/ Menus/ Styles/ Themes/   app resources, copied to output
  NativeMethods.* app.manifest Package.appxmanifest   MSBuild conventions, stay rooted
mpv-winui/mpv-winrt/        C++/WinRT component
  MpvPlayer.* MpvPreviewer.*   the runtime classes
  Types/  Events/             value types and event-arg types
  pch.*  *.vcxproj  .def      MSBuild conventions, stay rooted
mpv-winui-lazy/             config layer; mpv requires its main configs at the root
tools/                      self-checks (localization, settings drift, UI tooltips)
appdata-sample/             manual samples for %LOCALAPPDATA%\mpv-winui — see its README
docs/                       the settings design contract, audit, localization, upstream comparison, deploy layout
```

Two rules that bite when files move:

- **XAML moves are free but self-propagating.** A `.xaml` in a subfolder compiles to a
  `.xbf` at the same relative path, and the app loads pages by resource URI
  (`ms-appx:///Shell/MainWindow.xaml`). Move the source and the output follows; never
  hand-move a `.xbf` in the output.
- **MIDL resolves `import` relative to the project directory, not the importing file.**
  So `Types/MpvTrack.idl` must import `"Types/TrackType.idl"`, not `"TrackType.idl"`.
  Generated `.g.h` / `.winmd` are emitted by name into the intermediate directory, so
  source folders never change generated paths.

## Work loop

1. **Build** — run `.\build.ps1 -Configuration Release -Platform x64` from the repo root (VS MSBuild for the C++ component, then `dotnet build` for the app). Use full parameter names: `-Release x64` silently falls back to Debug.
   Done when: exit code 0, no error lines, and the app library exists at
   `mpv-winui\mpv-winui\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\mpvw.dll`.
   The assembly name is `mpvw`, not `mpv-winui` — `b777f30` added
   `<AssemblyName>mpvw</AssemblyName>`, which renamed the exe, the dll and the pri.
2. **Check** — run the self-checks in `tools/` (see below). This is the gate.
3. **Verify behaviour through MCP** — `tools/mvpw-mcp/` drives the deployed app
   and asserts control state, instead of a screenshot and an opinion.
4. **Hand over** — report the path of the built or deployed output and stop.

> **Run `python tools\check-xaml.py` *before* `build.ps1`, not after.** XAML
> cannot carry `--` inside a comment (WMC9997), and nothing in this repo's
> editing tools prevents you writing it: it has cost five full build cycles
> here, and on the fifth the build failed while the deploy still copied the
> previously built binaries and reported success. `check-xaml.py` catches it in
> about a second. If a deploy prints a healthy `identical: N` right after a
> build that failed, treat the deploy as stale, not as passing.

### Before committing

- **Remove debug instrumentation**: `grep -rn "\[DBG-"`.
- **Stage only repo files**: never `bin/`, `obj/`, `dist/`, `libs/`.
- **Check the BOM and the line endings against the index, not against the
  neighbouring files.** Neither is uniform here, and neither shows up usefully in
  a diff: a dropped BOM renders as `-﻿using` on line 1 while `git diff --stat`
  reports it as a one-character change, and a CRLF→LF flip renders as nothing
  until the next checkout rewrites it. `edit`-style tools drop the BOM; some
  Python scripts add one. Both directions have bitten this repo, a day apart.
  Trust `git diff --check` plus "no whole-file rewrite in `--stat`". Full rule in
  §5.6 of `docs/settings-design-contract.md`.

### Driving the app

**Current rule, in one place.** This section used to be four sections layered on top
of each other over three months, and the oldest read as a prohibition on exactly what
the newest allowed. It replaces all of them; the history is at the end.

1. **Launching is allowed.** Start `mpvw.exe` to look at it, to drive it over the
   MPV IPC pipe, and to run UI automation against it. Behaviour verification is
   expected, not merely tolerated: `tools/mvpw-mcp/` exists for it, and its
   verifiers are how a change is shown to work.
2. **It must never be left running.** Every script closes it and asserts the process
   is gone. This is the one rule that has never been relaxed.
3. **Close it with the switch, not `Stop-Process`:**
   `pwsh tools/deploy-test.ps1 -CloseRunning -Verify`. `-CloseRunning` posts
   `WM_CLOSE` first and force-kills only a window that has ignored it for 10 seconds,
   so WinUI shuts down and mpv flushes its log and saves state. Never deploy into a
   running installation.
4. **A screenshot proves appearance, not behaviour.** Use one for alignment,
   spacing, colour, clipping, or to diagnose a mis-render. Declaring a feature
   verified because it looks right is the specific mistake that once got screenshots
   banned here.
5. **The IPC pipe accepts one client at a time and drops it fast.** Corrected
   2026-10-26 by measurement, because the previous wording ("in roughly the first
   second after launch... a connection made later is refused") was wrong in both
   halves and produced a night of false "the probe is broken" conclusions:

   - **Connecting works at any time.** `CreateFile(\\.\pipe\mpvpipe)` succeeded at
     +1.05 s and again at +6.05 s on a fresh process. It is not a launch window.
   - **The accepted connection dies after roughly 0.4 s**, whether or not it is
     being used: with a `get_property` round-trip every 100 ms the writes started
     failing with `err=232` (`ERROR_NO_DATA`) at +1.42 s. So "keep it busy" does
     not keep it alive.
   - Only **one** client at a time; a second connect alternates `write 232` /
     `read 109` (`ERROR_BROKEN_PIPE`).

   Practical rule: connect and write **in the same breath**, do all the reading you
   need from that one burst, and take a fresh connection for the next burst. Never
   `sleep` between connect and the first write. `_work/probe/probe_pipe_lifetime2.py`
   measures all three facts in about 20 s.
   `mpv_tools.open_launch_window()` still works; it is just not the only time it can.

What works, measured on the deployed build:

- UI Automation reads this WinUI 3 window (`WinUIDesktopWin32WindowClass`, ~60
  AutomationIds: `ShellNav`, `ShellTitleText`, `ProgressSlider`, `PlayQueue`).
  `Grid` is not a UIA node, so a container you can see may have no element -- assert
  its children instead.
- mpv answers on `\\.\pipe\mpvpipe` while the app runs (`pause`, `volume`;
  `time-pos` is `property unavailable` until something is loaded, which is a valid
  answer, not a failure).
- Nav items needed ids to be addressable at all; they carry `ShellNav_<key>`
  (`AutomationProperties.SetAutomationId` -- `NavigationViewItem.AutomationId` does
  not compile).

```
python tools/mvpw-mcp/verify_e2e.py            # 19 steps, launch -> assert -> close
python tools/mvpw-mcp/verify_settings.py       # settings window opens and builds rows
python tools/mvpw-mcp/verify_mcp_protocol.py   # the server itself, over stdio JSON-RPC
```

The seven tools are `launch_app`, `get_ui_snapshot`, `invoke_control`,
`assert_element_state`, `screenshot_region`, `read_playback_state`,
`trigger_screenshot` (plus `close_app`). Register the server with:

```json
"mvpw": { "command": "python",
          "args": ["<repo>/tools/mvpw-mcp/mcpw_server.py"] }
```

Practical notes, each learned the hard way:

- Capture with `winapp ui screenshot -a <app>` when you can, and `CopyFromScreen` when
  you cannot; never `PrintWindow` on the main window and expect a flyout to appear.
  The mechanism is in Reference > Test facts: a WinUI flyout is a separate owned HWND.
- A whole-window screenshot can be evidence of a bug that is not there. The window
  drop shadow reads as a vertical strip of text at the left edge, and video content
  behind a panel reads as a render defect. Check the pixels before believing the
  picture. Five "defects" found by eye were refuted this way in one session.
- `verify_skins` asserts `transportUsable` (present and enabled), not "toggles
  pause": an asserted mechanism was refuted by measurement once already.

**History, so the contradiction is not reintroduced.** 2026-09-21 first banned
launching outright, after an agent treated a narrow permission as a general one.
Later that day screenshots were allowed (appearance only), then closing was allowed,
then on 2026-10-22 the MCP path allowed driving it properly -- because a
launch-to-assert loop is what catches the bugs. Every silent verifier failure fixed
in this repo was found by driving the app and none by reading it: a checker reporting
19/19 while its instrument was dead, an IPC client truncating its own handle, a
skinning verdict produced by a probe that could not run. The bans were right about the
failure mode (assert-and-conclude) and wrong about the remedy (never look). Rule 4 is
the part worth keeping.

### Reading a crash

A settings-window crash that reports `0xc000027b` + `Microsoft.UI.Xaml.dll` and
leaves no dialog, no managed stack and no log line is a stowed exception: the
exception escaped a click handler into WinUI's own dispatch, which catches it.

Look at the **`P7` field of the WER report first**. An `0x8013xxxx` value points
at managed code — `0x80131509` is `COR_E_INVALIDOPERATION`, i.e. an
`InvalidOperationException`, and `grep "throw new"` finds it in seconds. Do not
start by reading XAML.

The cheapest signal for this class is **asymmetric guarding between sibling
methods**: `OptionLayoutControl`'s `ToggleEdit` wrapped its work in try/catch and
its `Select` did not, and `Select` indexed `panel.Children[0]` with the guard its
neighbour used for index 1 (`Count > 1`, which does not cover 0). Diffing sibling
methods beats reading the file top to bottom.

## Reference

### Test facts

- IPC: `NamedPipeClientStream('.', 'mpvpipe')`; send JSON `{"command":[...],"request_id":N}`, read lines until the matching request_id.
- Logs: `%LOCALAPPDATA%\mpv-winui\mpv-winui\logs\mpv-winui.<date>.log.txt`, plus `display-info.log`.
  Note the **doubled** `mpv-winui`: `%LOCALAPPDATA%\mpv-winui\logs\` also exists and is a
  decoy -- its newest file is from 2026-09-25 while the live one is written every run. This
  line said the single-nesting path until 2026-10-26 and cost a session's worth of "the log
  is empty" before the real directory was found.
- mpv's own config dir is `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv\` (also doubled). Editing
  `%LOCALAPPDATA%\mpv-winui\mpv\mpv.conf` does nothing. And even in the right one, the
  options `MpvPlayer::Initialize` sets before `mpv_initialize` (`d3d11-output-mode`,
  `force-window`, `d3d11-composition-size`, `gpu-api`) **override** the file, so changing
  those there proves nothing -- change the C++ or the setting instead.
  `ConfigDeployer` re-copies the bundled layer each launch except its `UserOwnedFiles`
  (`mpv.conf`, `input.conf`, `menus.json`, `custom_menu.json`) and `UserOwnedDirs`
  (`script-opts`); `mpv/scripts/` IS re-deployed, so excluding scripts means renaming that
  directory, not `load-scripts=no`.
- Config runs from `%LOCALAPPDATA%\mpv-winui\mpv-winui\mpv` (synced by ConfigDeployer at startup).
- Settings are in registry `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app`; booleans stored as `"true"` / `"false"`.
- Defaults live in `AppSettings*.cs` (core + domain partials); live mpv mapping is `MpvSettings.ToCommand`.
- PrintWindow misses WinUI popups; the reason is API semantics, not a WinUI render
  defect. A WinUI flyout is its **own top-level HWND**
  (`className: "Microsoft.UI.Content.PopupWindowSiteBridge"`, `ownerHwnd` = the main
  window, `label: "popup"`), so `PrintWindow` on the main HWND cannot paint it --
  there is nothing wrong with the flyout. Measured 2026-10-27 with
  `winapp ui list-windows -a mpvw` after opening the sort flyout: two windows, the
  popup at 240x272 owned by 197366.
  Capturing options, in order of preference:
  1. `winapp ui screenshot -a <app>` -- composites the app's own windows **including
     owned popups**; its JSON lists both windows and reports `captured: true`, and the
     four flyout items came out legible. This is the only option that does not need the
     window in the foreground.
  2. `CopyFromScreen` -- works, but requires the window to be on screen, in front and
     unobscured, and it captures whatever is on top (which is how an unrelated
     full-screen game once got into a "the app is broken" screenshot).
  Do not use `PrintWindow` on the main window expecting popups to appear.
- **UIA BoundingRectangle is in physical pixels. There is no scale factor.**
  This line used to say "a ×1.5 DPI scale factor on this machine", which was wrong twice
  over: the factor here is **1.75** (168 DPI / 175%), and applying any factor to UIA
  coordinates is the mistake. Measured 2026-10-27 on mpvw hwnd 197366:
  `GetWindowRect` = 264,264,3144,1834 and `UIA BoundingRectangle` = 264,264,3144,1834 --
  identical, 2880x1570, with the measuring process made Per-Monitor-V2 aware. `winapp ui
  inspect` states the same thing in its own header: `windowDpi:168, scale:1.75,
  coordinateSpace:"physical-screen-pixels"`.
  The ×1.5 / ×1.75 myth has a traceable origin worth knowing, because it is a trap:
  a **DPI-unaware** caller (pwsh by default) asking `GetWindowRect` for the same window
  gets a virtualised 980x840 while UIA reports 1715x1470; the ratio is 1.75 and it looks
  exactly like "UIA adds a scale factor". It does not -- the caller was virtualised. Call
  `SetProcessDpiAwarenessContext(-4)` (or make the process PMv2) and the two agree.
  When you need DIPs: `DIP = physical / 1.75`. Never multiply UIA coordinates by a factor
  before feeding them back to `click`/`drag`/`touch`.

### Architecture gotchas

- **Native libraries are per architecture**: `libs\include` is shared, but
  `libs\lib\<Platform>\` and `libs\bin\<Platform>\` are not, and `$(Platform)` is
  the directory name verbatim. Both projects refuse to build when the target
  architecture's libraries are missing (`CheckNativeLibraries` in the vcxproj,
  `CheckNativeRuntimeLibraries` in mpv-winui.csproj); that guard replaced a
  silent success which produced a package containing no player. Win32 is gone
  for that reason, ARM64 is wired and waiting on binaries. Windows App SDK is
  referenced as the 2.5.1 meta-package with five components excluded via
  `ExcludeAssets` — bump them all together or NU1605 will tell you why.
  Rules and rationale: `docs/deploy-layout.md` §2.5.
- **Mouse input**: libmpv never sees Win32 mouse input in composition mode. The app forwards keyboard/mouse via the input-forwarding partial (MpvPlayerPage_InputForwarding.cs); display monitoring lives in MpvPlayerPage_Display.cs.
- **Command strings**: `mpv_command_string` parses C-style escapes. Windows paths need doubled backslashes inside quotes (`Q()` in `MpvSettings.cs`).
- **Command ordering**: there is none. `MpvPlayerPage.xaml.cs:147` sends
  `MpvSettings.BuildApplyAllCommands()` as one batch, and the open path calls
  `_mediaPlayer.OpenAsync(...)` directly from each caller
  (`MpvPlayerPage_History.cs:58,68`, `MpvPlayerPage_Library.cs:2170`,
  `MpvPlayerPage_Drop.cs`). Input forwarding is direct too.
  > This bullet used to say all three ran through `MpvCommandQueue` (a FIFO
  > worker). Nothing ever constructed it, and the file has since been deleted:
  > `Modules/Player/MpvCommandQueue.cs` does not exist, `git grep -n
  > 'MpvCommandQueue' -- '*.cs'` returns nothing, and `git log --all -S 'new
  > MpvCommandQueue'` finds no commit that ever did. Corrected 2026-10-22, and the
  > deletion recorded 2026-10-23 -- the earlier note still described the file as
  > present, which is the same rot this paragraph is about.
- **Position UI**: event-driven; `time-pos` observed natively + 100ms coalescing timer — never reintroduce polling.
- **Fullscreen**: driven by mpv's `fullscreen` property (`HandleFullscreenProperty`). State-based, never toggle-based. Always call `RefreshAdaptiveState()` after presenter transitions.
- **PiP**: dedicated borderless always-on-top window reusing `PlayerControl`. ExitPiP defers sizing until main window is shown and laid out (ActualWidth=0 bug).
- **Overlay bar**: activity-based visibility with 1.5s idle timer. ControlPanelGradient Height=120 only in overlay mode.
- **Control-bar layout**: `ApplyBarOrders` rebuilds three CommandBars from canonical lists on every apply — never filter through current items.
- **Control-bar overflow**: all three CommandBars set `OverflowButtonVisibility="Collapsed"`. `CommandBar` draws its *own* `⋯` beside the app's `MoreButton`, so an un-suppressed bar shows two adjacent overflow buttons and one of them is empty. The app's `MoreButton` appears only when `ControlBarAdaptiveLayout.HasOverflowContent` says the menu has something to offer — the same table `MoreFlyout_Opening` builds the menu from, which is what stops it opening an empty menu. Add an entry to `OverflowEntries` to add one to both.
- **Control-bar tiers**: `ControlBarAdaptiveLayout.TierFor` takes width *and* height, because a 384-wide portrait window is above `NarrowWidth` and otherwise never reaches the portrait tier. Portrait keeps nine slots: three left, previous/play/next in the middle, and on the right the overflow button plus two icons. It tightens the button pitch (`IsTight`) rather than dropping controls, except `aspect`, which goes to the overflow menu. These rules are pure and linked into `mpv-conf-test` — run the tests, do not verify this one by eye.
- **Player popup chrome**: the volume strip, track selector, quick-control panel and More menu share one chrome — `MpvPlayerPopupPresenterStyle` and `MpvPlayerPopupCornerRadius` in `Styles.xaml`, plus a shadow depth that has to be a literal (`Translation="0,0,32"` at PlayerControl.xaml:941 and :964) because `Translation` is a `Vector3` and XAML has no resource form for one. A third key, `MpvPlayerPopupShadowDepth`, existed until 2026-10-22 with zero readers; it was deleted rather than wired, because a token nothing can read is worse than a literal that says why it is one. The volume strip and the More menu are Flyouts and consume the style. The track selector and the control panel are `Popup`s with `ShouldConstrainToRootBounds="False"`, because a Flyout is clipped to the window and both have to be able to leave it; they reproduce the same brushes, radius and `ThemeShadow` on their own `Border`. **Change those together.** Anchor every one of them with `PlacementTarget` + `DesiredPlacement`/`Placement="Top"` — never compute window coordinates by hand, which is what once put the panel under the window instead of above its button. Because the two Popups are unconstrained, nothing pulls them back on screen, so `PlayerControl.ControlPanel.PlacementFor` reads the window's `DisplayArea.WorkArea` and bends `Top` to `TopEdgeAlignedLeft`/`Right` when centring would run the popup off the display.
- **Playback modes**: single mode button cycles 不循环→顺序→单曲循环→随机. RepeatButton hidden; ShuffleButton reused. Glyphs F175/F172/EF34/EF37.

### Settings architecture

**`docs/settings-design-contract.md` is the authority for anything under
`Modules/Settings/`.** Read it before changing the settings tree; it carries the
rules, the reasons and a 33-entry register of known exceptions. What follows is
the orientation, not a substitute.

- `SettingsPage.Options.cs`: tree builder (`BuildSettings`) with four tables —
  `sectionMap` (every row's section, the only source), `sectionOrder`,
  `optionOrder`, plus `categoryOrder`. A row with no `sectionMap` entry makes
  `BuildSettings` throw on open (fail-loud, deliberately).
- **The tree has two halves.** Hand-written rows live in the `Options.*.cs`
  partials; 356 schema-generated rows live in `SettingsPage.Options.Gen.cs`
  (`tools/gen-schema-options.py`). Edit the generator or `MpvConfOptions/mpv-options.json`,
  never a `.Gen.` file — the next run overwrites it. Anything that enumerates
  options, sections or mpv names must read **both** halves; six checkers did not,
  and reported tens of thousands of phantom findings between them.
- `Controls/Option*.xaml/cs`: one renderer per `OptionType`. `OptionType` (11
  members) → `OptionTemplateSelector` switch → `OptionListControl` `DataTemplate`
  is a 1:1:1 mapping; adding a type means all three.
- A folded section is described in three places that must stay in sync: the row
  flag `Option.AdvancedSection`, the page's `FoldedSections` set, and
  `OptionListControl.CollapsedSectionIds`. The last is a **sticky** dependency
  property, so every branch that assigns `OptionList` must clear it.
- Dependency gating: `RefreshWarningsAndEnabled()` evaluates Warning/IsEnabled/IsVisible rules.
- `tools/check-settings-drift.py`: validates AppSettings ↔ ToCommand ↔ option keys.
  `UNMAPPED_OK` is the list of settings that change what the app does and can
  never be an mpv option. **Silence a genuine app-only finding by adding it there
  with a reason** — never by adding a fake `ToCommand` arm, which would write app
  state into `mpv.conf`.
- `tools/check-localization.py`: validates AppLang properties × the nine
  `Languages/*.json`, reading all three `AppLang*.cs` partials.
- `tools/check-sections.py`: sections, icons, sort keys, and the sectionMap
  guard mirrored at commit time.

### Cross-language contracts that are checked

Three defects in this repo had one shape: **two things agreed to a contract,
nothing compared them, and the break was silent.** Each now has a checker, and
the rule for adding the next one is written down here because the pattern
recurred more often than the specific bugs did.

| Contract | Checker |
|---|---|
| C# event subscription ↔ C++ `m_*Event` raise | `check-winrt-events.py` |
| Local Lua script ↔ `script-opts` the pinned mpv sends it | `check-script-opts.py` |
| `AppLang` property ↔ nine language files | `check-localization.py` |
| `AppSettings` ↔ `ToCommand` ↔ `MpvConfOptionNames` | `check-settings-drift.py`, `check-conf-lines.py` |

The three that bit hardest:

- **`PlaybackFailed` / `VoConfigured`.** `c49b461` renamed the native raises;
  the IDL kept the old declarations, so both C# subscriptions compiled and never
  fired. `MediaPlayer_MediaFailed` never ran and the `Error` visual state was
  unreachable.
- **`populate_menu_data`.** `mpv-2.dll` hardcodes
  `script-opt=select-populate_menu_data=no` for its bundled `select.lua`; the
  project's localized copy declared two of the five options, so every launch
  logged "unknown key … ignoring" and dropped the value.
- **`MpvSpaceXS`.** A `{StaticResource}` name that differed from the key by one
  character threw inside `InitializeComponent()`, which `Frame.Navigate`
  swallows — the symptom was an empty window with no dialog and no log line.

**When adding a checker, three rules:**

1. **See it fail first.** Inject the real defect, not a synthetic one — the two
   above were replayed by reintroducing the actual subscription line. A checker
   that has never been red is a checker whose blind spot is unknown.
2. **Refuse unconvincing input.** "Parsed zero items" must be an error, not a
   clean bill of health; otherwise a regex that stops matching reports success
   forever (`check-script-opts` exits 2 when the DLL yields no `script-opt`
   lines).
3. **Ask what the code reads, not what a comment says it does.** Six checkers
   here were green while measuring the wrong half of the tree.

**Beware the reverse-test that cannot fail.** The first version of the
script-opts reverse test replaced `"…,\n"` in a CRLF file, so the injection
never applied and every case reported the baseline — indistinguishable from a
passing checker.

### Right-click menu

- Built from dyn_menu.lua's `menu-data` property via `BuildMenuFlyoutFromData()`.
- Items appear in input.conf annotation order (same as mpv-menu-plugin).
- No icons, no filtering, no renames — plain text rendering.
- `HiddenMenuTitles` was removed; to hide entries edit input.conf directly.
- The native C++ builder (`MpvMenuBuilder.cpp`) was removed (it was disabled
  after a ta.c canary crash; re-enable paths live in git history).

### Thumbnail preview (seek-bar)

- In-process second libmpv instance (`MpvPreviewer.cpp`) with software rendering.
- Thumbfast-style: async keyframe seeks gated on `seeking` flag; demuxer cache (32MiB fwd/16MiB back); FILE_LOADED deferred seek.
- C# warms up on MediaOpened; min position delta 0.25s.
- Adjustable: size (180/248/320px) and update interval (40–600ms) in settings.

### License guardrails

- App code: LGPL-2.1. Project-written config-layer files: LGPL-2.1-or-later.
- dyn_menu.lua is GPL-2.0-only source with a documented local patch; dialog.lua
  was removed (no menu references it anymore) — see
  `mpv-winui-lazy/scripts/LOCAL-PATCHES.md`.
- Update `mpv-winui-lazy/licenses/THIRD_PARTY_NOTICES.md` when adding third-party components.

## Pointers

- **Fixing UI defects over many rounds: start at `docs/ui-repair/README.md`.** It is a
  self-contained brief for a long-running repair task — the loop, what may be decided
  without asking, the evidence rules, and the tool chain in `tools/ui-repair/`. The queue
  it drives is `docs/ui-repair/QUEUE.md`; the reasoning authority is
  `docs/ui-repair/BASELINE.md`.
- **Changing anything under `Modules/Settings/`: read
  `docs/settings-design-contract.md` first.** It is the single authority for the
  settings UI — structure, naming and grouping rules, UI and interaction
  constraints, the mandatory verification flow, and a register of every known
  exception. Where an older doc disagrees with it, that doc is out of date.
- Editing menus / input.conf: read `docs/localization.md` first.
- Localization: all user-facing strings go through AppLang × Languages/*.json (9 languages). Run `python tools/check-localization.py` after changes.
- Adding settings options: read the design contract and `docs/localization.md`;
  run the settings checkers afterwards — at minimum `check-sections`,
  `check-localization`, `check-settings-drift`, `check-empty-sections` and
  `diagnose-advanced-flag`.
- A checker that has never been seen to fail is not evidence. Before trusting a
  new or edited one, inject a known violation and confirm it goes red — and ask
  whether it reads what the *code* reads, or what a comment says the code does.
  Six checkers here were green while measuring the wrong half of the tree.