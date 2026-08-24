# Local modifications to GPL scripts

`dyn_menu.lua` is GPL-2.0-only source from tsl0922/mpv-menu-plugin. The GPL
boundary matters here (see AGENTS.md): the file stays an *unmodified upstream
file plus a documented patch*, never rewritten in place, and no GPL binary
(menu.dll) is shipped. (`dialog.lua` from the same source was removed — no
menu references it anymore.)

## dyn_menu.lua — project-local patch

The file is `menu-plugin`'s `dyn_menu.lua` with these local additions
(diff-friendly markers: sections between `-- ===== 菜单本地化 =====` and
`-- 播放列表按钮专用菜单（本地补丁）` are the project patch):

1. **Menu localization** (`-- ===== 菜单本地化 =====`, ~line 18):
   reads `user-data/mpvw/language` (written by the app) and translates the
   `#menu:` titles via the embedded 7-language `menu_i18n` /
   `dyn_prefix_i18n` tables; observes the property and re-localizes on
   change (line ~1734).
2. **Playlist/audio button menus** (`-- 本地补丁`, ~line 1604):
   dedicated playlist/audio menus that used the removed `menu.dll` Win32
   temporary channel; kept as pure-Lua now.
3. Misc: menu-data shaping that feeds the WinUI right-click menu
   (`user-data/menu/items` fallback).

To re-base on a newer upstream `dyn_menu.lua`:

```
git diff --no-index <upstream-dyn_menu.lua> scripts/dyn_menu.lua > dyn_menu.patch
# apply the patch onto the new upstream file, resolve conflicts, re-run
# the localization check and right-click menu smoke.
```

## Why this matters

`THIRD_PARTY_NOTICES.md` documents the GPL carve-out: the WinUI app talks
to these scripts only via public `menu-data` / `script-message`
interfaces, so the GPL does not extend to the app. Keeping the patch as a
reviewable delta preserves that boundary and makes license audits
mechanical.
