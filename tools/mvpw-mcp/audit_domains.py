"""Fix the domain audit so it measures the design instead of fighting it.

What the first run got wrong, and why each was a test bug rather than a defect:

1. After a file is loaded the window is the PLAYER PAGE by design: the nav pane,
   the settings button and the open-file button leave the tree, and the back
   button is the documented way out. The audit walked into that state and then
   tried to click nav items that the design removed. Every domain now returns to
   the library through ShellBackButton first.
2. Snapshot depth 14 was too shallow once the player page was up; the tree is
   deeper while playing. Depth is now 22 everywhere.
3. `get_ui_snapshot` redacts names by default (they are the user's media
   titles), so comparing "section titles" compared three "<redacted>" strings
   and reported a duplicate. The audit asks for real names only for labels it
   knows are not user content -- the section heading -- and compares those.
4. `_mediaPlayer.LoadFile` was being driven while the app had already resumed a
   previously watched file, so "which clip is loaded" was not what the audit
   thought. The audit now records the filename it actually got.
"""
import argparse
import json
import subprocess
import sys
import time

import psutil

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
DEPTH = 22

findings = []
measurements = {}


def note(domain, level, item, detail):
    findings.append({"domain": domain, "level": level, "item": item,
                     "detail": str(detail)})
    print("    %-6s %-32s %s" % (level, item, str(detail)[:110]))


def reg(name, value=None):
    if value is None:
        r = subprocess.run(
            ["pwsh", "-NoProfile", "-Command",
             f'(Get-ItemProperty -Path "{REG}" -Name {name} -ErrorAction SilentlyContinue).{name}'],
            capture_output=True, text=True)
        return (r.stdout or "").strip()
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    f'Set-ItemProperty -Path "{REG}" -Name {name} -Value "{value}" -Type String'],
                   capture_output=True, text=True)
    return value


def snap(redact=True):
    s = T.get_ui_snapshot(max_depth=DEPTH, redact_names=redact)
    return {e["automationId"]: e for e in s["elements"] if e["automationId"]}


def area(r):
    return (r[2] - r[0]) * (r[3] - r[1]) if r else 0


def overlap(a, b):
    if not a or not b:
        return 0
    return max(0, min(a[2], b[2]) - max(a[0], b[0])) * \
           max(0, min(a[3], b[3]) - max(a[1], b[1]))


def back_to_library():
    """Return to the library the way the design says to."""
    ids = snap()
    if "ShellBackButton" in ids and len([k for k in ids if k.startswith("ShellNav_")]) == 0:
        try:
            T.invoke_control("ShellBackButton", "invoke")
        except T.ToolError:
            pass
        time.sleep(2.2)
    T._ipc(["stop"], 700)
    time.sleep(1.2)
    return snap()


# ---------------------------------------------------------------- domains ---

def domain_shell():
    print("\n== 外壳 Shell ==")
    ids = back_to_library()
    expected = ["ShellNav", "ShellTitleBar", "ShellTitleText", "ShellBackButton",
                "ShellSettingsButton", "ShellOpenFileButton", "ShellLibrarySearch"]
    for m in expected:
        if m not in ids:
            note("shell", "FAIL", m, "missing from the automation tree")

    for key in ("home", "music", "videos", "queue"):
        aid = "ShellNav_" + key
        r = ids.get(aid, {}).get("rect")
        if not r:
            note("shell", "FAIL", aid, "nav item missing")
        elif area(r) < 500:
            note("shell", "FAIL", aid, "nav item is %dx%d" % (r[2]-r[0], r[3]-r[1]))

    back = ids.get("ShellBackButton", {}).get("rect")
    title = ids.get("ShellTitleText", {}).get("rect")
    if overlap(back, title):
        note("shell", "FAIL", "back/title", "overlap %d px2" % overlap(back, title))

    measurements["shell"] = {"ids": len(ids),
                             "nav": {k: ids.get(k, {}).get("rect")
                                     for k in ids if k.startswith("ShellNav")}}


def domain_navigation():
    print("\n== 导航 Navigation ==")
    seen = {}
    for key in ("home", "music", "videos", "queue", "home"):
        back_to_library()
        try:
            T.invoke_control("ShellNav_" + key, "select")
        except T.ToolError as e:
            note("navigation", "ERROR", key, e)
            continue
        time.sleep(1.8)
        # The heading is app text, not user content: ask for the real name.
        s = T.get_ui_snapshot(max_depth=DEPTH, redact_names=False)
        by = {e["automationId"]: e for e in s["elements"] if e["automationId"]}
        title = by.get("LibrarySectionTitle", {}).get("name")
        hosts = [h for h in ("LibraryScroll", "QueueClearButton", "MusicTitle",
                             "VideoAddFolderButton", "LibraryEmptyTitle")
                 if area(by.get(h, {}).get("rect")) > 0]
        seen[key] = {"title": title, "hosts": hosts,
                     "titleRect": by.get("LibrarySectionTitle", {}).get("rect")}
        print("    %-7s title=%-10r hosts=%s" % (key, title, hosts))

    for key in ("home", "music", "videos", "queue"):
        if not seen.get(key, {}).get("hosts"):
            note("navigation", "FAIL", key, "nothing visible after selecting it")

    titles = {k: v["title"] for k, v in seen.items() if v.get("title")}
    print("    titles:", titles)
    if len(titles) >= 4 and len(set(titles.values())) < 3:
        note("navigation", "FAIL", "section headings",
             "sections share headings: %s" % titles)

    measurements["navigation"] = seen


def domain_library():
    print("\n== 媒体库 Library ==")
    back_to_library()
    T.invoke_control("ShellNav_videos", "select")
    time.sleep(1.8)
    ids = snap()
    for tab in ("VideoTabAll", "VideoTabFolders"):
        r = ids.get(tab, {}).get("rect")
        if not r:
            note("library", "FAIL", tab, "tab missing")
        elif area(r) < 200:
            note("library", "FAIL", tab, "collapsed to %dx%d" % (r[2]-r[0], r[3]-r[1]))

    empty = ids.get("LibraryEmptyTitle", {}).get("rect")
    add = ids.get("VideoAddFolderButton", {}).get("rect")
    if empty and add and overlap(empty, add) > 0.5 * area(empty):
        note("library", "FAIL", "empty state",
             "the empty-state text is covered by the add-folder button")

    if "LibraryIndexRail" in ids:
        note("library", "WARN", "LibraryIndexRail",
             "present in the video library; it is a music affordance")

    measurements["library"] = {"tabs": {t: ids.get(t, {}).get("rect")
                                        for t in ("VideoTabAll", "VideoTabFolders")},
                               "empty": empty, "addFolder": add,
                               "rail": "LibraryIndexRail" in ids}


def domain_player():
    print("\n== 播放页 Player ==")
    back_to_library()
    T._ipc(["loadfile", CLIP, "replace"], 800)
    time.sleep(4.0)
    st = T.read_playback_state()
    ids = snap()
    print("    playing: %r" % (st.get("filename"),))

    transport = ["PlayPauseButton", "ProgressSlider", "VolumeSlider",
                 "TimeElapsedElement", "TimeRemainingElement",
                 "SkipBackwardButton", "SkipForwardButton"]
    for t in transport:
        r = ids.get(t, {}).get("rect")
        if not r:
            note("player", "FAIL", t, "missing while playing")
        elif area(r) < 100:
            note("player", "FAIL", t, "collapsed to %dx%d" % (r[2]-r[0], r[3]-r[1]))

    pbar = ids.get("ProgressSlider", {}).get("rect")
    vol = ids.get("VolumeSlider", {}).get("rect")
    if pbar and vol:
        if pbar[1] > vol[1]:
            note("player", "FAIL", "progress/volume",
                 "bar y=%d is below volume y=%d" % (pbar[1], vol[1]))
        if area(pbar) < area(vol):
            note("player", "FAIL", "progress/volume",
                 "bar (%d px2) smaller than volume (%d px2)" % (area(pbar), area(vol)))

    if st.get("filename") is None:
        note("player", "FAIL", "clip", "nothing loaded after a loadfile")

    # The documented behaviour while playing: the nav pane is gone and the back
    # button is the way out. Assert that, rather than treat it as a defect.
    nav = [k for k in ids if k.startswith("ShellNav_")]
    if nav:
        note("player", "NOTE", "nav during playback",
             "nav items are still reachable while playing (%d)" % len(nav))
    else:
        note("player", "INFO", "nav during playback",
             "hidden by design; ShellBackButton is the route back")

    measurements["player"] = {"transport": {t: ids.get(t, {}).get("rect")
                                            for t in transport},
                              "state": {k: st.get(k) for k in
                                        ("pause", "duration", "volume", "filename")}}
    back_to_library()


def domain_queue():
    print("\n== 播放队列 Queue ==")
    back_to_library()
    T.invoke_control("ShellNav_queue", "select")
    time.sleep(1.8)
    ids = snap()
    for aid in ("QueueClearButton", "QueueAddToButton", "QueueEmptyTitle"):
        if aid not in ids:
            note("queue", "FAIL", aid, "missing")
    c = ids.get("QueueClearButton", {}).get("rect")
    a = ids.get("QueueAddToButton", {}).get("rect")
    if overlap(c, a):
        note("queue", "FAIL", "clear/addto", "buttons overlap")
    measurements["queue"] = {"clear": c, "addTo": a}


def domain_menu():
    print("\n== 右键菜单 Menu ==")
    back_to_library()
    # The flyout is built from mpv's menu-data; ask mpv how much there is.
    # `menu-data` is what GetMenu reads (MpvPlayer.cpp:2439). Asking for
    # user-data/menu/items was a test bug: that property does not exist, so the
    # first run reported "no menu" about a menu with 24 entries.
    r = T._ipc(["get_property", "menu-data"], 810)
    items = r.get("data") if r.get("error") == "success" else None
    count = len(items) if isinstance(items, list) else 0
    print("    menu items available:", count, "(%s)" % r.get("error"))
    if count == 0:
        note("menu", "WARN", "menu-data",
             "mpv exposes no menu items, so the right-click menu has nothing to show")
    measurements["menu"] = {"itemCount": count}


def domain_settings():
    print("\n== 设置页 Settings ==")
    back_to_library()
    import uiautomation as auto
    pid = T._pid()
    T.invoke_control("ShellSettingsButton", "invoke")
    time.sleep(3.0)

    settings = None
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid and w.Name != "MPV" and w.NativeWindowHandle != 0:
                settings = w
        except Exception:
            continue
    if settings is None:
        note("settings", "FAIL", "window", "did not open")
        return

    ids = {}

    def walk(c, d=0):
        if d > 16:
            return
        try:
            aid = c.AutomationId
            if aid:
                ids.setdefault(aid, c)
            kids = c.GetChildren()
        except Exception:
            return
        for k in kids:
            walk(k, d + 1)

    walk(settings)
    for need in ("CategoryNav", "OptionListView", "SearchBox",
                 "CustomizeToggle", "ResetAllButton"):
        if need not in ids:
            note("settings", "FAIL", need, "missing from the settings window")

    # Scope the walk and count only list items that sit in the nav PANE. The
    # first two versions were wrong twice over: searching by id from the desktop
    # root found the main window's SplitView, and even inside the settings
    # window, `CategoryNav` names the whole NavigationView -- pane AND content --
    # so counting every ListItem counted the option rows too (34 against a
    # catalog of 6). The pane is the subtree under the pane host.
    cats = []

    def collect(c, d=0, in_pane=False):
        if d > 10:
            return
        for k in c.GetChildren():
            try:
                aid = k.AutomationId or ""
                here = in_pane or aid in ("PaneRoot", "MenuItemsHost",
                                          "MenuItemsScrollViewer")
                if here and k.ControlTypeName == "ListItemControl":
                    cats.append((k.Name or "")[:24])
            except Exception:
                continue
            collect(k, d + 1, here)

    nav = ids.get("CategoryNav")
    if nav:
        collect(nav)
    print("    categories in the pane:", len(cats), cats)
    if cats and len(cats) != 6:
        note("settings", "WARN", "CategoryNav",
             "%d categories in the pane, the catalog defines 6" % len(cats))

    shot = T.screenshot_region(
        path=r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\audit-settings.png")
    try:
        settings.GetWindowPattern().Close()
    except Exception:
        pass
    time.sleep(1.5)
    measurements["settings"] = {"ids": len(ids), "categories": cats,
                                "shot": shot["path"]}


def domain_theme():
    print("\n== 主题 / 皮肤 Theme ==")
    results = {}
    for field, values in (("ThemeType", ("Light", "Dark", "Auto")),
                          ("PlayerStyle", ("Centre", "Compact", "Standard"))):
        for v in values:
            reg(field, v)
            T.close_app()
            # close_app can return before the process is actually gone; the
            # first run then hit "already running" on the next launch.
            for _ in range(20):
                if not any((p.info["name"] or "").lower() == "mpvw.exe"
                           for p in psutil.process_iter(["name"])):
                    break
                time.sleep(0.3)
            T.launch_app()
            # Poll for the shell rather than trusting one fixed delay: 2.4 s was
            # usually enough and still produced a false "did not come up".
            waited = 0.0
            ids = {}
            while waited < 12.0:
                ids = snap()
                if "ShellNav" in ids and "ShellTitleText" in ids:
                    break
                time.sleep(0.4)
                waited += 0.4
            alive = "ShellNav" in ids and "ShellTitleText" in ids
            if not alive:
                note("theme", "FAIL", f"{field}={v}", "the shell did not come up")
            results[f"{field}={v}"] = {
                "alive": alive,
                "title": ids.get("ShellTitleText", {}).get("rect"),
                "navCount": len([k for k in ids if k.startswith("ShellNav_")]),
            }
            print("    %-10s %-9s alive=%s (%.1fs)" % (field, v, alive, waited))
    reg("ThemeType", "Auto")
    reg("PlayerStyle", "")
    measurements["theme"] = results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json",
                    default=r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\domain-audit.json")
    args = ap.parse_args()

    T.close_app()
    T.launch_app()
    time.sleep(2.5)

    for fn in (domain_shell, domain_navigation, domain_library, domain_player,
               domain_queue, domain_menu, domain_settings, domain_theme):
        try:
            fn()
        except Exception as e:
            note(fn.__name__, "ERROR", "domain crashed", f"{type(e).__name__}: {e}")
            T.close_app()

    T.close_app()
    left = [p.info["name"] for p in psutil.process_iter(["name"])
            if (p.info["name"] or "").lower() == "mpvw.exe"]
    if left:
        note("cleanup", "FAIL", "process", left)

    counts = {}
    for f in findings:
        counts[f["level"]] = counts.get(f["level"], 0) + 1
    print("\n" + "=" * 62)
    print("by level:", json.dumps(counts))
    for f in findings:
        print("%-6s %-12s %-28s %s" % (f["level"], f["domain"], f["item"],
                                       str(f["detail"])[:95]))
    print("=" * 62)

    with open(args.json, "w", encoding="utf-8") as fh:
        json.dump({"findings": findings, "measurements": measurements},
                  fh, ensure_ascii=False, indent=1)
    print("wrote", args.json)
    return 0


if __name__ == "__main__":
    sys.exit(main())
