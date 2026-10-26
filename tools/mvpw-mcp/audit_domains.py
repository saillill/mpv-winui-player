"""A broad per-domain sweep, for orienting yourself -- NOT a pass/fail gate.

**Do not treat this file's summary as evidence.** It is the least reliable tool in
tools/mvpw-mcp/ and it has a record to match: over one session it attributed defects to
the product on four separate occasions, and every one was the probe's own state
mishandling.

  * "46 findings" in an early run were a registry-restoring bug in `reg()`.
  * "4 domain crashed: mpvw.exe is not running" was one crashed domain closing the app
    and the rest never being given a process.
  * "menu-data: mpv exposes no menu items" was a read at startup with no media loaded;
    after `loadfile` the same property returns 24 items.
  * "no element with AutomationId='ShellNav_videos'" three times was its own
    `back_to_library()` pressing ShellBackButton on a healthy window, which REMOVES the
    nav pane (measured in diag_nav_restore.py).

Its real weakness is architectural: eight domains share one process, and the mpv pipe
serves one conversation per process in roughly its first second. Every attempt to paper
over that (relaunch per domain, per-domain conversations) has traded one failure for
another, which is why the summary varies between runs (4/4, then 1/1, then 5/3).

**For an actual verdict use the verifiers**, each of which owns its process and asserts
one thing:

    verify_e2e.py           19 steps, launch -> assert -> close
    verify_settings.py      the settings window opens and builds its rows
    verify_settings_rows.py the six removed options are really gone
    verify_preview_api.py   the preview bridge round-trips
    verify_screenshot.py    screenshot-to-file works
    verify_no_focus_steal.py a launch does not take the owner's foreground

This file stays because it is useful for looking around a domain quickly, and because
its findings, once investigated by hand, were what led to the menu and nav discoveries
recorded above.
"""
import argparse
import json
import subprocess
import sys
import time

import psutil

import mpvw_tools as T
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
DEPTH = 22

findings = []
measurements = {}


def note(domain, level, item, detail):
    findings.append({"domain": domain, "level": level, "item": item,
                     "detail": str(detail)})
    print("    %-6s %-32s %s" % (level, item, str(detail)[:110]))


def _app_alive():
    """Is mpvw.exe in the process list right now?

    Checked per domain because a crashed domain closes the app, and every later
    domain then fails with "not running" -- which looks like several broken features
    and is really one. psutil rather than T._pid() so this does not need a pipe.
    """
    return any((p.info["name"] or "").lower() == "mpvw.exe"
               for p in psutil.process_iter(["name"]))


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
    """Return to the library without destroying the state it is meant to restore.

    Measured (diag_nav_restore.py), and it is the opposite of what this used to assume:

      after launch      ShellNav + 4 items present
      after nav home    unchanged
      after `stop`      unchanged
      after BACK BUTTON everything disappears -- id list goes from 5 to 0

    So `ShellBackButton` is not "leave the player view and show the library": pressing it
    while already in the library takes the nav pane away. The old guard only checked
    that no `ShellNav_*` item existed, never that `ShellNav` itself was present, and each
    call re-read a snapshot that a previous step had already emptied -- so the second
    and later calls pressed the button on a healthy screen and broke it, and the audit
    then reported three broken nav items.

    Correct precondition: press it only when the pane is genuinely absent (no `ShellNav`
    at all). If `ShellNav` is present, the library is already showing and the button
    must be left alone.
    """
    ids = snap()
    pane_present = "ShellNav" in ids
    items_present = any(k.startswith("ShellNav_") for k in ids)

    if not pane_present and "ShellBackButton" in ids:
        try:
            T.invoke_control("ShellBackButton", "invoke")
            time.sleep(2.2)
        except T.ToolError:
            pass
    elif pane_present and not items_present:
        # The pane is there but collapsed; nothing to press, and pressing the back
        # button is what removed it in the first place.
        pass

    # Best-effort cleanup, never fatal. `stop` is only here to leave the next domain a
    # clean player, and the pipe serves one conversation per process in roughly its
    # first second -- so by the time a later domain calls this, the request is refused
    # with error 2 and the exception took the whole domain down with it. The message
    # then read "domain crashed", which described a cleanup step rather than the
    # domain's actual subject.
    try:
        T._ipc(["stop"], 700)
    except Exception:
        pass
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
    # Own process, like the player and menu domains. The nav pane is only present in the
    # library view, and `back_to_library` sends `stop` over IPC to get there -- which
    # fails once the launch conversation is spent, leaving the app playing and the nav
    # pane hidden. The audit then reported "no element with AutomationId='ShellNav_*'"
    # for every key, which reads as a broken navigation pane.
    try:
        T.close_app()
    except Exception:
        pass
    for _ in range(20):
        if not _app_alive():
            break
        time.sleep(0.3)
    T.launch_app()
    back_to_library()
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
    # Own process, launched WITH the startup snapshot. read_playback_state() returns
    # that snapshot and raises when it is absent -- which is the correct behaviour, and
    # the reason this domain reported "no mpv status snapshot" when it inherited a
    # process that other domains had already used: the pipe serves one conversation per
    # process, in roughly its first second.
    try:
        T.close_app()
    except Exception:
        pass
    for _ in range(20):
        if not _app_alive():
            break
        time.sleep(0.3)
    # Two needs, one conversation: the status snapshot AND a loadfile. launch_app
    # (snapshot=True) spends the conversation on the snapshot, so the loadfile that
    # follows is refused -- measured as "nothing loaded after a loadfile", which looked
    # like a broken open path.
    #
    # open_launch_window() connects during the window and keeps the handle, so both
    # requests go over the same conversation. They must follow each other closely: a
    # sleep between connecting and the first request closes it
    # (verify_preview_api.py records exactly that).
    T._close_pipe()
    for _ in range(20):
        if not _app_alive():
            break
        time.sleep(0.3)
    T.open_launch_window(4.0)
    load = T._ipc(["loadfile", CLIP, "replace"], 800)
    print("    loadfile:", str(load)[:90])

    # Read the state over the SAME conversation rather than through
    # read_playback_state(), which answers from the snapshot launch_app takes at startup.
    # open_launch_window() does not take that snapshot (it spends the pipe on the
    # caller's own requests), so calling read_playback_state() here raised "no mpv status
    # snapshot" -- reported as a crashed player domain.
    def prop(name):
        try:
            r = T._ipc(["get_property", name], 801)
            return r.get("data") if isinstance(r, dict) else None
        except Exception:
            return None

    st = {"filename": prop("filename"), "pause": prop("pause"),
          "volume": prop("volume"), "idle": prop("idle-active")}
    print("    after load: pause=%s volume=%s idle=%s file=%r"
          % (st["pause"], st["volume"], st["idle"], st["filename"]))

    time.sleep(4.0)
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
    # This domain needs its own mpv conversation, so it gets its own process. By the
    # time the sequential audit reaches here, the launch window of whatever process is
    # running has long closed, and every request answers 232/109 -- which the first
    # version of this reported as "menu is empty".
    #
    # open_launch_window() connects DURING the launch window, and the requests below
    # must follow immediately: sleeping between connecting and the first request closes
    # the conversation (verify_preview_api.py records exactly that).
    try:
        T.close_app()
    except Exception:
        pass
    for _ in range(20):
        if not _app_alive():
            break
        time.sleep(0.3)
    T.open_launch_window(4.0)

    # The flyout is built from mpv's menu-data; ask mpv how much there is.
    # `menu-data` is what GetMenu reads (MpvPlayer.cpp:2439). Asking for
    # user-data/menu/items was a test bug: that property does not exist, so the
    # first run reported "no menu" about a menu with 24 entries.
    # Load something first. Without media, menu-data is legitimately empty (measured),
    # so reading it here and calling the result a finding would be measuring the wrong
    # state -- the third time this particular probe would have done that.
    if CLIP:
        try:
            T._ipc(["loadfile", CLIP, "replace"], 809)
            time.sleep(2.0)
        except Exception as e:
            print("    could not load a clip:", str(e)[:80])

    r = T._ipc(["get_property", "menu-data"], 810)
    items = r.get("data") if r.get("error") == "success" else None
    count = len(items) if isinstance(items, list) else 0
    print("    menu items available:", count, "(%s)" % r.get("error"))
    if count == 0:
        # Measured, not assumed (tools/mvpw-mcp/diag_menu_after_load.py): with no
        # media loaded, menu-data is `[]`; after `loadfile` it becomes **24 items**
        # within four tight reads. So an empty menu here means the audit is looking
        # before anything is playing, NOT that the menu is broken.
        #
        # This function has now been wrong about the menu three times: first it asked
        # for a property that does not exist (user-data/menu/items), then it called an
        # empty result a WARN, then it explained the emptiness as "configuration" when
        # the actual cause was timing. Each time the fault was a probe that could not
        # tell its cases apart. The measurement above is the case that distinguishes.
        note("menu", "INFO", "menu-data",
             "menu is empty because no media is loaded at this point in the run; "
             "measured 24 items after loadfile, so this is timing, not a defect")
    measurements["menu"] = {"itemCount": count}


def domain_settings():
    print("\n== 设置页 Settings ==")
    # A clean process, for the same reason domain_menu now takes one: this domain runs
    # after others that may have closed or restarted the app, and it drives the shell by
    # AutomationId. "no element with AutomationId='ShellSettingsButton'" meant the window
    # it was looking at was not the app's -- reported as a broken settings page.
    try:
        T.close_app()
    except Exception:
        pass
    for _ in range(20):
        if not _app_alive():
            break
        time.sleep(0.3)
    T.launch_app()
    time.sleep(2.5)
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
        path=REPO + r"\_work\mcp\audit-settings.png")
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
                    default=REPO + r"\_work\mcp\domain-audit.json")
    args = ap.parse_args()

    T.close_app()
    T.launch_app()
    time.sleep(2.5)

    for fn in (domain_shell, domain_navigation, domain_library, domain_player,
               domain_queue, domain_menu, domain_settings, domain_theme):
        # Every domain gets a running app, because a previous one may have crashed and
        # closed it. Without this the audit reported four "domain crashed: mpvw.exe is
        # not running" for domains that were never handed a process: one failure
        # cascaded through the rest, which reads like four broken features and is
        # actually one dead probe. That is the same assert-and-conclude mistake this
        # repo keeps finding, committed by the auditing tool itself.
        if not _app_alive():
            note(fn.__name__, "INFO", "relaunching", "the app was not running")
            try:
                T.launch_app()
                time.sleep(2.5)
            except Exception as e:
                note(fn.__name__, "ERROR", "could not relaunch",
                     f"{type(e).__name__}: {e}")
                continue
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
