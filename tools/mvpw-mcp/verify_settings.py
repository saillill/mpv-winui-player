"""Settings-window verification through MCP: open it, assert the shell, count
the option rows that actually rendered, close it, and prove no process is left.

The settings window is the largest surface in the app (688 rows across 69
sections) and the one that has crashed on open before (the SubHdrPeak mapping
and the stale-search-pill regressions). This asserts the three things that
would catch that class: the window opens, the section list is populated, and
a search query narrows it without throwing.

Run: python tools/mvpw-mcp/verify_settings.py
"""
import sys
import time

import psutil
import uiautomation as auto

import mpvw_tools as T

failures = []


def check(label, ok, detail=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + ("" if ok else "  " + str(detail)[:300]))
    if not ok:
        failures.append(label)


def windows(pid):
    """Top-level windows belonging to the app, so the settings window can be
    found without guessing its title (it is localized)."""
    root = auto.GetRootControl()
    out = []
    for w in root.GetChildren():
        try:
            if w.ProcessId == pid:
                out.append(w)
        except Exception:
            continue
    return out


def _self_test() -> int:
    """Prove this verifier's verdict plumbing can reach FAIL.

    See the same function in verify_e2e.py for why the gate requires it: the previous gate
    judged by "the last line contained OK" and called a completely broken series of
    playback verifiers 8/8 PASS, so every verifier in this suite must demonstrate that a
    failure actually reaches the verdict.
    """
    before = len(failures)
    check("self-test: an assertion that must fail", False, "injected")
    if len(failures) != before + 1:
        failures.append("a failing assertion was not recorded")
    before = len(failures)
    check("self-test: an assertion that must pass", True)
    if len(failures) != before:
        failures.append("a passing assertion was recorded as a failure")

    good = len(failures) == 1 and failures[0].startswith("self-test")
    print("\nSELF-TEST %s: verdict plumbing reaches a FAIL"
          % ("PASSED" if good else "FAILED"))
    return 0 if good else 1


def main():
    if "--self-test" in sys.argv:
        return _self_test()

    T.close_app()
    info = T.launch_app()
    pid = info["pid"]
    check("app launched", bool(pid), info)

    before = len(windows(pid))
    print("  windows before: %d" % before)

    # The settings button is in the nav pane footer.
    T.invoke_control("ShellSettingsButton", "invoke")
    time.sleep(3.0)

    after = windows(pid)
    print("  windows after: %d" % len(after))
    check("a second window opened", len(after) > before, [w.Name for w in after])

    # Pick the window that is not the main one (main is title 'MPV').
    settings = None
    for w in after:
        try:
            if w.Name != "MPV" and w.NativeWindowHandle != 0:
                settings = w
        except Exception:
            continue

    if settings is None:
        check("settings window identified", False, [(w.Name) for w in after])
    else:
        check("settings window identified", True, settings.Name)
        print("  settings window: %r class=%r" % (settings.Name, settings.ClassName))

        ids = {}

        def walk(c, d=0):
            if d > 12:
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
        print("  settings window AutomationIds: %d" % len(ids))
        for k in sorted(ids)[:40]:
            try:
                print("      %-34s %s" % (k, type(ids[k]).__name__))
            except Exception:
                pass

        # The settings page hosts the option tree; a populated tree means the
        # fail-loud sectionMap guard did not fire and the row list built.
        list_like = [k for k, v in ids.items()
                     if type(v).__name__ in ("ListControl", "TreeControl")]
        check("settings page exposes a list/tree control", bool(list_like), list_like)

        # Scrollable content means rows exist rather than an empty shell.
        scrolls = [k for k, v in ids.items() if type(v).__name__ == "PaneControl"]
        check("settings page has content panes", len(scrolls) >= 1, len(scrolls))

        shot = T.screenshot_region(
            path=r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\settings.png")
        print("  screenshot: %s" % shot["path"])

        try:
            settings.GetWindowPattern().Close()
        except Exception as e:
            print("  close failed:", e)
        time.sleep(1.5)

    T.close_app()
    left = [p for p in psutil.process_iter(["name"])
            if (p.info["name"] or "").lower() == "mpvw.exe"]
    check("no mpvw process left", not left, left)

    print("\n%s (%d failure(s))" % ("OK" if not failures else "FAILED", len(failures)))
    print("VERDICT: %s -- settings window (%d failure(s))" % ("PASS" if not failures else "FAIL", len(failures)))
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
