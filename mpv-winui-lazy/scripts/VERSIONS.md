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
| `dynamic_menu.lua` | dynamic VSR/HDR submenus |
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

### "bundled copy" is not a pin — measured 2026-10-21

Of the 15 scripts in this directory, **one** carries a resolvable commit
(`stats.lua`). The rest carry no `COMMIT_`, and two of the three that shadow an
mpv built-in (`select.lua`, `console.lua`) are marked only "bundled copy".
That phrasing recorded an intention, not a version, and it cost real drift:

`mpv.conf` ships `script-opt=select-populate_menu_data=no` — that line is inside
`mpv-2.dll`'s default profile. mpv's own `select.lua` declares five script
options; this copy declared two. So every launch logged

```
[mpv/select] script-opts: unknown key populate_menu_data, ignoring
```

and dropped the value. Nothing broke, which is why it survived: the option
happened not to be load-bearing here.

**The rule this run establishes:** a script that shadows an mpv built-in must
declare every script-opt mpv sends it, and that is now enforced by
`tools/check-script-opts.py` (wired into `build.ps1` and CI's
`Check script-opts coverage`). It reads the keys straight out of the pinned
`mpv-2.dll`, so an mpv bump that widens the gap fails the build instead of
adding a log line.

**Why a commit pin is still worth adding, and what it would buy.** The checker
above covers the script-opts contract only. It cannot see behavioural drift in
the parts of the file this project rewrote. A pin would let the upgrade diff be
`git diff <pin>..<new>` instead of "read both files and hope". Doing that for
`select.lua` and `console.lua` needs the upstream commit that each copy was cut
from, which is not recoverable from the files themselves — it has to come from
whoever performed the localisation. Until then the honest statement is: two
shadowing scripts are at an unknown upstream revision, guarded on the one axis
that can be checked mechanically.

| Shadowing script | script-opts contract | upstream revision |
|---|---|---|
| `stats.lua` | checked | pinned (`COMMIT_`) |
| `select.lua` | **checked** | unknown — localisation source not recorded |
| `console.lua` | checked | unknown — localisation source not recorded |

## GPL-2.0-only (license boundary — see AGENTS.md)

| Script | Source | Note |
|---|---|---|
| `dyn_menu.lua` | https://github.com/tsl0922/mpv-menu-plugin | project-local mods; keep as a diff (`dialog.lua` from the same source was removed — no menu references it) |

## Upgrading a pinned script

1. Download the upstream file at the new commit.
2. Diff against the bundled copy; record the diff in the commit message.
3. Update the `COMMIT_`/pin in the file header **and** this table.
4. Run `python tools/check-script-opts.py` — an upstream bump can add script
   options that the local copy then has to declare.
5. Re-run `deploy-config.ps1` and smoke the affected feature.

> VapourSynth templates (`vs/`) and fonts come from
> mpv-lazy / upstream projects with their own provenance; see
> `../licenses/THIRD_PARTY_NOTICES.md` for licenses.
