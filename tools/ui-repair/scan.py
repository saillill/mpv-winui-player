"""Find real defects to seed the repair queue, using measurements rather than guesses.

The queue has to start with items SHOWN to be wrong. This repo's history is full of
"obvious" defects that turned out to be probe bugs, so seeding it by reading code would
poison the whole task.

Element shape from get_ui_snapshot (measured, not assumed):
    {depth, automationId, type, nameLength, name, enabled, rect}
    rect = [left, top, right, bottom]

Checks, each printing the value that decided it:
  layout   rect outside the window, or a named control with zero area
  a11y     interactive control with no accessible name
  spacing  vertical gaps between named siblings that match no MpvSpace* token

**Units, and the bug the first version had.** UIA `BoundingRectangle` reports DEVICE
PIXELS; the `MpvSpace*` tokens in Styles.xaml are DIP. Comparing one to the other quantises
every gap by the display scale, so on a 175% display each token appears at 1.75x its
declared value and no real gap can ever match. Measured here: `GetDpiForWindow` returns 168
(scale 1.75), and `LibrarySectionSubtitle`, which is authored `Margin="0,8,0,0"`, measures
14 px -- exactly 8 x 1.75. So `check_spacing` converts to DIP before comparing, and the
scale is measured from the window rather than assumed from the doc.

**Siblinghood is inferred from `depth`, which is wrong, and the output says so.** The
snapshot carries no parentId (see `check_spacing`'s own note), so same-depth is used as a
proxy. It is not siblinghood: `ShellTitleText`, `PaneRoot` and `LibrarySectionTitle` sit at
the same depth in different containers, so their apparent gaps are cross-container and carry
no meaning. The check therefore re-walks the live tree to get TRUE parents and reports only
gaps between real siblings as findings; depth-only candidates are printed separately as
"unconfirmed" so the distinction is visible instead of buried.

Findings are candidates. One becomes a queue item only after a second, different check
agrees -- which is what the two-run discipline in PLAYBOOK.md is for.
"""
import collections
import ctypes
import io
import json
import sys
from ctypes import wintypes

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import mpvw_tools as T

TOKEN_VALUES = {2, 4, 6, 8, 12, 16}   # MpvSpaceXxs/Xs/Compact/S/M/L

INTERACTIVE = {"ButtonControl", "CheckBoxControl", "RadioButtonControl",
               "ComboBoxControl", "SliderControl", "EditControl",
               "MenuItemControl", "TabItemControl", "HyperlinkControl",
               "ListItemControl", "ToggleButtonControl"}

# Tolerance in DIP. A layout gap is a sum of authored values and text-line remainders, so
# an exact match is not available; 0.5 DIP is below a device pixel at any supported scale,
# so anything inside it cannot be told apart from the token.
DIP_TOLERANCE = 0.5


def display_scale():
    """Device pixels per DIP for the app window, measured from the window's own DPI."""
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    user32.GetDpiForWindow.restype = wintypes.UINT
    try:
        hwnd = T._window().NativeWindowHandle
        dpi = user32.GetDpiForWindow(wintypes.HWND(hwnd))
        if dpi:
            return dpi / 96.0, dpi
    except Exception:
        pass
    return 1.0, 96


def true_parents():
    """automationId -> parent automationId, from the live tree.

    The snapshot has no parentId, and equal `depth` is not siblinghood -- at depth 4 the
    live tree puts ShellTitleText (inside a TitleBar), PaneRoot (a Window) and
    LibrarySectionTitle (inside a navigation pane) in one list. This walks once to recover
    the real edges.
    """
    parents = {}
    try:
        win = T._window()
    except Exception:
        return parents

    def walk(ctrl, parent, depth=0):
        if depth > 26:
            return
        try:
            aid = ctrl.AutomationId or ""
            kids = ctrl.GetChildren()
        except Exception:
            return
        if aid:
            parents[aid] = parent
        for k in kids:
            walk(k, aid or parent, depth + 1)

    walk(win, None)
    return parents



def snapshot():
    return T.get_ui_snapshot(max_depth=22, redact_names=False)["elements"]


def window_rect():
    r = T._window().BoundingRectangle
    return [r.left, r.top, r.right, r.bottom]


def check_layout(els, wr):
    wl, wt, wrr, wb = wr
    out = []
    for e in els:
        r = e.get("rect")
        if not r or len(r) != 4:
            continue
        l, t, rr, b = r
        w, h = rr - l, b - t
        aid = e.get("automationId") or ""
        if w < 0 or h < 0:
            out.append(("negative", aid, w, h))
        elif (w == 0 or h == 0) and aid:
            out.append(("zero-area", aid, w, h))
        elif aid and (l < wl - 2 or t < wt - 2 or rr > wrr + 2 or b > wb + 2):
            out.append(("outside", aid, "%dx%d" % (w, h), r))
    return out


def check_a11y(els):
    out = []
    for e in els:
        if (e.get("type") or "") not in INTERACTIVE:
            continue
        if not (e.get("name") or "").strip():
            out.append(((e.get("type") or "")[:22],
                        e.get("automationId") or "(no id)", e.get("rect")))
    return out


def check_spacing(els, scale, parents):
    """Vertical gaps between TRUE siblings, in DIP, compared against token values.

    Two corrections, both from measurement:

      * `parentId` does not exist in the snapshot, so same-depth was used as a sibling
        proxy. Measured, it is not one: `ShellTitleText` (a TitleBar child) and `PaneRoot`
        (a Window) and `LibrarySectionTitle` (inside ShellNav) all report depth 4. Their
        apparent gaps -- 23px in the old output -- are between unrelated containers and
        mean nothing. Real parents come from `true_parents()`; anything that is not a real
        sibling is reported as unconfirmed instead of as a finding.
      * UIA rects are device pixels and MpvSpace* tokens are DIP. Without the conversion
        every token appears at `scale` times its declared value (14px/1.75 = 8 DIP, i.e.
        MpvSpaceS), so on a scaled display nothing could ever match a token.

    Anything owning its own metrics (nav items, command bar buttons) is out of scope by
    construction, not by wishful thinking.
    """
    FRAMEWORK_OWNED = ("ShellNav_", "ShellNav", "MenuBar", "ContentElement")

    rows = [e for e in els if e.get("automationId") and e.get("rect")
            and len(e["rect"]) == 4]
    rows.sort(key=lambda e: (e.get("depth", 0), e["rect"][1], e["rect"][0]))

    confirmed = collections.Counter()
    unconfirmed = collections.Counter()

    for a, b in zip(rows, rows[1:]):
        if a.get("depth") != b.get("depth"):
            continue
        aid_a, aid_b = a["automationId"], b["automationId"]
        if any(x in aid_a for x in FRAMEWORK_OWNED):
            continue
        if any(x in aid_b for x in FRAMEWORK_OWNED):
            continue
        gap_px = b["rect"][1] - a["rect"][3]
        h_overlap = min(a["rect"][2], b["rect"][2]) - max(a["rect"][0], b["rect"][0])
        if not (0 < gap_px <= 40 * scale and h_overlap > 0):
            continue

        dip = gap_px / scale
        if any(abs(dip - tok) <= DIP_TOLERANCE for tok in TOKEN_VALUES):
            continue

        # Real siblings only. The snapshot's depth proxy lets A->B and B->C pair up even
        # when B is in another container; requiring the same real parent removes that.
        if parents.get(aid_a) is not None and parents.get(aid_a) == parents.get(aid_b):
            confirmed[(round(dip, 1), aid_a, aid_b)] += 1
        else:
            unconfirmed[(round(dip, 1), aid_a, aid_b)] += 1

    return confirmed, unconfirmed


def main():
    T.close_app()
    T.launch_app()
    els = snapshot()
    wr = window_rect()
    scale, dpi = display_scale()
    parents = true_parents()
    print("elements: %d" % len(els))
    print("window  : %s (%dx%d)" % (wr, wr[2] - wr[0], wr[3] - wr[1]))
    print("scale   : %.2f px/DIP (GetDpiForWindow=%d)  tokens compared in DIP"
          % (scale, dpi))
    print("parents : %d named controls resolved from the live tree" % len(parents))

    findings = {}

    layout = check_layout(els, wr)
    print("\nlayout problems: %d" % len(layout))
    for kind, aid, a, b in layout[:12]:
        print("   %-10s %-28s %s %s" % (kind, aid, a, b))
    if layout:
        findings["layout"] = layout

    a11y = check_a11y(els)
    print("\nunnamed interactive controls: %d" % len(a11y))
    for t, aid, rect in a11y[:12]:
        print("   %-22s %s" % (t, aid))
    if a11y:
        findings["a11y"] = [[t, a] for t, a, _ in a11y]

    confirmed, unconfirmed = check_spacing(els, scale, parents)
    print("\nvertical gaps between TRUE siblings, no MpvSpace token: %d"
          % len(confirmed))
    for (dip, a, b), n in sorted(confirmed.items()):
        print("   %5.1f DIP  %-24s -> %-24s  x%d" % (dip, a[:24], b[:24], n))
    if confirmed:
        findings["spacing"] = [{"dip": d, "from": a, "to": b, "count": n}
                               for (d, a, b), n in sorted(confirmed.items())]

    # Kept visible rather than dropped: these come from the depth proxy, and the point of
    # printing them separately is that depth is not siblinghood -- the old output reported
    # four of these AS findings, and all four were cross-container.
    print("\nunconfirmed (same depth, different real parent -- not a finding): %d"
          % len(unconfirmed))
    for (dip, a, b), n in sorted(unconfirmed.items()):
        print("   %5.1f DIP  %-24s -> %-24s  x%d" % (dip, a[:24], b[:24], n))
    if unconfirmed:
        findings["spacingUnconfirmed"] = [
            {"dip": d, "from": a, "to": b, "count": n}
            for (d, a, b), n in sorted(unconfirmed.items())]

    T.close_app()
    out = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\ui-repair-scan.json"
    io.open(out, "w", encoding="utf-8").write(
        json.dumps(findings, indent=2, ensure_ascii=False))
    print("\nwritten:", out)


if __name__ == "__main__":
    sys.exit(main())
