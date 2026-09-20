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
3. **Hand over** — report the path of the built or deployed output and stop.

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

### Do not launch the app

**Do not start `mpvw.exe`, and do not write or run UI-automation probes against
it.** The person this project is built for tests it themselves and has said so
explicitly: UI probes "have never once worked", and a launch-to-assert round trip
costs more than it proves. The test for whether an action is allowed is a single
question: **would it put `mpvw.exe` in the process list? If yes, don't.**

So the work loop stops at "builds clean and the self-checks pass". Do not drive
the app over the mpv IPC pipe, do not take screenshots of it, and do not deploy
into a running installation.

> This loop previously told agents to launch the exe, drive it over the IPC pipe
> and verify by screenshot. That contradicted the instruction above, and because
> this file is what an agent reads first, it was read as permission. Corrected
> 2026-09-21.

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
- Logs: `%LOCALAPPDATA%\mpv-winui\logs\mpv-winui.<date>.log.txt`, plus `display-info.log`.
- Config runs from `%LOCALAPPDATA%\mpv-winui\mpv` (synced by ConfigDeployer at startup).
- Settings are in registry `HKCU\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app`; booleans stored as `"true"` / `"false"`.
- Defaults live in `AppSettings*.cs` (core + domain partials); live mpv mapping is `MpvSettings.ToCommand`.
- PrintWindow misses WinUI popups; use CopyFromScreen for visual verification.
- UIA BoundingRectangle has a ×1.5 DPI scale factor on this machine.

### Architecture gotchas

- **Mouse input**: libmpv never sees Win32 mouse input in composition mode. The app forwards keyboard/mouse via the input-forwarding partial (MpvPlayerPage_InputForwarding.cs); display monitoring lives in MpvPlayerPage_Display.cs.
- **Command strings**: `mpv_command_string` parses C-style escapes. Windows paths need doubled backslashes inside quotes (`Q()` in `MpvSettings.cs`).
- **Command ordering**: settings ApplyAll, menu commands and `OpenAsync` run through `MpvCommandQueue` (FIFO worker); input forwarding stays direct.
- **Position UI**: event-driven; `time-pos` observed natively + 100ms coalescing timer — never reintroduce polling.
- **Fullscreen**: driven by mpv's `fullscreen` property (`HandleFullscreenProperty`). State-based, never toggle-based. Always call `RefreshAdaptiveState()` after presenter transitions.
- **PiP**: dedicated borderless always-on-top window reusing `PlayerControl`. ExitPiP defers sizing until main window is shown and laid out (ActualWidth=0 bug).
- **Overlay bar**: activity-based visibility with 1.5s idle timer. ControlPanelGradient Height=120 only in overlay mode.
- **Control-bar layout**: `ApplyBarOrders` rebuilds three CommandBars from canonical lists on every apply — never filter through current items.
- **Control-bar overflow**: all three CommandBars set `OverflowButtonVisibility="Collapsed"`. `CommandBar` draws its *own* `⋯` beside the app's `MoreButton`, so an un-suppressed bar shows two adjacent overflow buttons and one of them is empty. The app's `MoreButton` appears only when `ControlBarAdaptiveLayout.HasOverflowContent` says the menu has something to offer — the same table `MoreFlyout_Opening` builds the menu from, which is what stops it opening an empty menu. Add an entry to `OverflowEntries` to add one to both.
- **Control-bar tiers**: `ControlBarAdaptiveLayout.TierFor` takes width *and* height, because a 384-wide portrait window is above `NarrowWidth` and otherwise never reaches the portrait tier. Portrait keeps a 3/3/3 grid and tightens the button pitch (`IsTight`) rather than dropping controls. These rules are pure and linked into `mpv-conf-test` — run the tests, do not verify this one by eye.
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
  partials; 548 schema-generated rows live in `SettingsPage.Options.Gen.cs`
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
