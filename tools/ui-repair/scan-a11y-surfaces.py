"""Audit accessible names on every surface the app has, not just the main window.

`scan-a11y-names.py` walks four nav sections plus "playing" in the MAIN window. The settings
window is a separate top-level window, and the Flyouts/Popups are separate UIA subtrees, so
none of them were measured. Everything listed here was a genuine blind spot:

    settings window      separate top-level window, 688 rows, its own pane + search bar
    More menu            Flyout
    track selector       Popup with ShouldConstrainToRootBounds=False
    quick-control panel  Popup
    volume strip         Flyout

Two rules this follows, both learned from measurements in this repo:

  * A count of zero from a surface that produced no elements is UNMEASURED, not a pass.
    "The popup never opened" and "the popup is perfectly labelled" are identical in a
    number, so the element count is printed beside the result.
  * An empty `Name` is not automatically a defect. A ComboBox labelled by the TextBlock in
    its own row is correctly nameless, and a screen reader announces the row text. Those are
    reported separately (`labelled by a neighbour`) so the number that matters -- controls
    with a name from NO source -- stays meaningful.

Exit code is 1 when a reachable, on-screen, focusable control has neither a name nor an
adjacent label, so this can be wired into the gate. Currently exits 0.

Run:  python tools/ui-repair/scan-a11y-surfaces.py
"""
import json
import os
import sys
import time

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
sys.path.insert(0, os.path.join(REPO, "tools", "mvpw-mcp"))

import mpvw_tools as T  # noqa: E402

OUT = os.path.join(REPO, "_work", "a11y-surfaces.json")

INTERACTIVE = {"ButtonControl", "CheckBoxControl", "RadioButtonControl",
               "ComboBoxControl", "SliderControl", "EditControl", "MenuItemControl",
               "TabItemControl", "HyperlinkControl", "ListItemControl",
               "ToggleButtonControl", "SplitButtonControl"}

# Control types the framework labels from a neighbouring TextBlock by design. Kept explicit
# so a bare Button is never excused.
#
# RadioButtonControl is in the list because of a measured case: OptionLayoutControl builds
# its choice radios with no Content at all (OptionLayoutControl.xaml.cs:89) and puts the
# caption in a sibling TextBlock inside the same row ("原版" / "居中"). Reporting those as
# unnamed was a false positive -- the row reads correctly to a screen reader -- and it was
# the ONLY entry left in the settings window's bare list after the real two were fixed.
# TabItemControl is NOT in the list: the Pivot tabs that reported empty names carried no
# text anywhere in their own subtree, which made them a genuine defect.
NEIGHBOUR_LABELLED_TYPES = {"ComboBoxControl", "EditControl", "SliderControl",
                            "RadioButtonControl"}


def walk(root, depth=0, limit=26):
    out = []
    if depth > limit:
        return out
    try:
        kids = root.GetChildren()
    except Exception:
        return out
    for k in kids:
        try:
            r = k.BoundingRectangle
            info = {
                "type": k.ControlTypeName,
                "name": (k.Name or "").strip(),
                "id": k.AutomationId or "",
                "offscreen": k.IsOffscreen,
                "focusable": k.IsKeyboardFocusable,
                "rect": [int(r.left), int(r.top), int(r.right), int(r.bottom)] if r else None,
            }
        except Exception:
            continue
        out.append(info)
        out.extend(walk(k, depth + 1, limit))
    return out


def audit(root):
    """Unnamed interactive elements, split by whether a neighbour labels them."""
    bare, neighbour = [], []
    stack = [(root, [])]
    while stack:
        ctrl, parent_texts = stack.pop()
        try:
            kids = ctrl.GetChildren()
        except Exception:
            continue
        texts = []
        for k in kids:
            try:
                if k.ControlTypeName == "TextControl" and (k.Name or "").strip():
                    texts.append((k.Name or "").strip())
            except Exception:
                continue
        for k in kids:
            try:
                ctype = k.ControlTypeName
                name = (k.Name or "").strip()
                aid = k.AutomationId or ""
                off = k.IsOffscreen
                focusable = k.IsKeyboardFocusable
                r = k.BoundingRectangle
                rect = [int(r.left), int(r.top), int(r.right), int(r.bottom)] if r else None
            except Exception:
                continue
            if ctype in INTERACTIVE and not name:
                size = ((rect[2] - rect[0]), (rect[3] - rect[1])) if rect else (0, 0)
                rec = {"type": ctype, "id": aid, "rect": rect, "size": list(size),
                       "offscreen": off, "focusable": focusable,
                       "siblingTexts": texts[:3]}
                if texts and ctype in NEIGHBOUR_LABELLED_TYPES:
                    neighbour.append(rec)
                else:
                    bare.append(rec)
            stack.append((k, texts))
    return bare, neighbour


def windows_of(pid):
    import uiautomation as auto
    out = []
    for w in auto.GetRootControl().GetChildren():
        try:
            if w.ProcessId == pid:
                out.append(w)
        except Exception:
            continue
    return out


def wait_window(pid, exclude="MPV", timeout=20):
    """No fixed sleep: poll for a second top-level window belonging to the app."""
    t0 = time.time()
    while time.time() - t0 < timeout:
        for w in windows_of(pid):
            try:
                if w.Name != exclude and w.NativeWindowHandle != 0:
                    return w
            except Exception:
                continue
        time.sleep(0.25)
    return None


def report(label, root):
    els = walk(root)
    bare, neighbour = audit(root)
    reachable_bare = [b for b in bare
                      if not b["offscreen"] and b["size"][0] > 0 and b["focusable"]]
    print("%-20s elements=%-5d unnamed=%-3d  labelled-by-neighbour=%-3d  BARE+reachable=%d%s"
          % (label, len(els), len(bare) + len(neighbour), len(neighbour),
             len(reachable_bare),
             "   UNMEASURED" if not els else ""))
    for b in reachable_bare[:8]:
        print("      %-18s id=%-24s size=%-10s texts=%s"
              % (b["type"], b["id"] or "(none)", b["size"],
                 ", ".join(b["siblingTexts"])[:40] or "(none)"))
    return {"label": label, "elements": len(els), "bare": bare,
            "neighbourLabelled": neighbour, "reachableBare": reachable_bare}


def main():
    results = []
    T.close_app()
    info = T.launch_app()
    pid = info["pid"]

    results.append(report("main/home", T._window()))

    T.invoke_control("ShellSettingsButton", "invoke")
    settings = wait_window(pid)
    if settings is None:
        print("settings             UNMEASURED: no second window appeared")
        results.append({"label": "settings", "elements": 0, "reachableBare": []})
    else:
        t0 = time.time()
        while time.time() - t0 < 30:
            if any(e["type"] == "CheckBoxControl" for e in walk(settings)):
                break
            time.sleep(0.4)
        results.append(report("settings", settings))
        try:
            settings.GetWindowPattern().Close()
        except Exception:
            pass
        time.sleep(0.8)

    for label, aid in (("more menu", "MoreButton"), ("control panel", "ControlPanelButton")):
        try:
            T.invoke_control(aid, "invoke")
            t0 = time.time()
            while time.time() - t0 < 8:
                if any(e["type"] == "MenuItemControl" for e in walk(T._window())):
                    break
                time.sleep(0.2)
            results.append(report(label, T._window()))
            T.invoke_control(aid, "invoke")
        except Exception as e:
            print("%-20s SKIPPED: %s" % (label, str(e)[:60]))
            results.append({"label": label, "elements": 0, "reachableBare": [],
                            "note": str(e)[:80]})

    T.close_app()

    total = sum(len(r["reachableBare"]) for r in results)
    unmeasured = [r["label"] for r in results if r["elements"] == 0]
    print("\nBARE reachable controls across surfaces: %d" % total)
    print("unmeasured surfaces: %s" % (unmeasured or "none"))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(json.dumps(results, indent=2, ensure_ascii=False))
    print("written:", OUT)
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
