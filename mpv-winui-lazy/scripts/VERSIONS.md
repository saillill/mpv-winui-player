# Script provenance & pinned versions

Every Lua script in `scripts/` must be traceable to a source and, where
upstream is actively maintained, pinned to a commit so upgrades are
reviewable diffs instead of silent drift. The pin is the source of truth;
the file header comment repeats it.

## Project-local (no upstream, LGPL-2.1-or-later)

| Script | Note |
|---|---|
| `hdr_auto.lua` | RTX Video HDR auto-attach |
| `vsr_auto.lua` | NVIDIA VSR auto-attach |
| `seek_hold.lua` | freeze window resize during seeks |
| `dynamic_menu.lua` | dynamic shader/VSR/HDR submenus |
| `mpvw_hdr_override.lua` | color-kind manual override |
| `save_global_props.lua` | persist volume/mute |
| `auto_sub_fonts_dir.lua` | auto-load adjacent fonts dir |
| `stats_mediainfo.lua` | MediaInfo OSD (derived from mpv-lazy discussion #624) |

## Upstream, pinned

| Script | Source | Pin |
|---|---|---|
| `stats.lua` | https://github.com/mpv-player/mpv (player/lua/stats.lua) | commit in header `COMMIT_` |
| `select.lua` | https://github.com/mpv-player/mpv (player/lua/select.lua) | bundled copy, Chinese-localized |
| `console.lua` | https://github.com/mpv-player/mpv (player/lua/console.lua) | bundled copy, WinUI-styled (reads Windows light/dark) |
| `coverart.lua` | https://github.com/CogentRedTester/mpv-coverart | MIT |
| `recentmenu.lua` | https://github.com/natural-harmonia-gropius/recent-menu | MIT |
| `metadata_osd.lua` | mpv-lazy bundle | version 0.6.2 in header |

## GPL-2.0-only (license boundary — see AGENTS.md)

| Script | Source | Note |
|---|---|---|
| `dyn_menu.lua` | https://github.com/tsl0922/mpv-menu-plugin | project-local mods; keep as a diff (`dialog.lua` from the same source was removed — no menu references it) |

## Upgrading a pinned script

1. Download the upstream file at the new commit.
2. Diff against the bundled copy; record the diff in the commit message.
3. Update the `COMMIT_`/pin in the file header **and** this table.
4. Re-run `deploy-config.ps1` and smoke the affected feature.

> Shaders (`shaders/`), VapourSynth templates (`vs/`) and fonts come from
> mpv-lazy / upstream projects with their own provenance; see
> `THIRD_PARTY_NOTICES.md` for licenses.
