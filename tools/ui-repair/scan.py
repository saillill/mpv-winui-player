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

Findings are candidates. One becomes a queue item only after a second, different check
agrees -- which is what the two-run discipline in PLAYBOOK.md is for.
"""
import collections
import io
import json
import sys

sys.path.insert(0, r"C:\Users\z2637\Documents\DeepSeek\mpv\tools\mvpw-mcp")

import mpvw_tools as T

TOKEN_VALUES = {2, 4, 6, 8, 12, 16}   # MpvSpaceXxs/Xs/Compact/S/M/L

INTERACTIVE = {"ButtonControl", "CheckBoxControl", "RadioButtonControl",
               "ComboBoxControl", "SliderControl", "EditControl",
               "MenuItemControl", "TabItemControl", "HyperlinkControl",
               "ListItemControl", "ToggleButtonControl"}


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


def check_spacing(els):
    """Vertical gaps between same-depth, vertically adjacent named siblings.

    Two corrections over the first version, both from measurement:

      * `parentId` does not exist in the snapshot. The only structural hint is `depth`,
        so same-depth is used as the sibling proxy. Without it the check compared
        ShellTitleText against ShellNav (23px) and ShellSettingsText against
        ProgressSlider (39px) -- different levels entirely, and neither a spacing
        problem.
      * ShellNav_home -> ShellNav_music is 7px and IS same-level, but NavigationView
        owns that pitch internally; the app cannot set it from a token. Items the
        framework lays out are excluded by name prefix.

    A finding here is a candidate: something the app positions with a bare number where a
    token exists. Anything owning its own metrics (nav items, command bar buttons) is out
    of scope by construction, not by wishful thinking.
    """
    FRAMEWORK_OWNED = ("ShellNav_", "ShellNav", "MenuBar", "ContentElement")

    rows = [e for e in els if e.get("automationId") and e.get("rect")
            and len(e["rect"]) == 4]
    rows.sort(key=lambda e: (e.get("depth", 0), e["rect"][1], e["rect"][0]))

    odd = collections.Counter()
    for a, b in zip(rows, rows[1:]):
        if a.get("depth") != b.get("depth"):
            continue
        if any(x in (a["automationId"] or "") for x in FRAMEWORK_OWNED):
            continue
        if any(x in (b["automationId"] or "") for x in FRAMEWORK_OWNED):
            continue
        gap = b["rect"][1] - a["rect"][3]
        h_overlap = min(a["rect"][2], b["rect"][2]) - max(a["rect"][0], b["rect"][0])
        if 0 < gap <= 40 and h_overlap > 0 and gap not in TOKEN_VALUES:
            odd[(gap, a["automationId"], b["automationId"])] += 1
    return odd


def main():
    T.close_app()
    T.launch_app()
    els = snapshot()
    wr = window_rect()
    print("elements: %d" % len(els))
    print("window  : %s (%dx%d)" % (wr, wr[2] - wr[0], wr[3] - wr[1]))

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

    gaps = check_spacing(els)
    print("\nvertical gaps matching no MpvSpace token: %d" % len(gaps))
    for (v, a, b), n in sorted(gaps.items()):
        print("   %3dpx  %-24s -> %-24s  x%d" % (v, a[:24], b[:24], n))
    if gaps:
        findings["spacing"] = [{"gap": v, "from": a, "to": b, "count": n}
                               for (v, a, b), n in sorted(gaps.items())]

    T.close_app()
    out = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\ui-repair-scan.json"
    io.open(out, "w", encoding="utf-8").write(
        json.dumps(findings, indent=2, ensure_ascii=False))
    print("\nwritten:", out)


if __name__ == "__main__":
    sys.exit(main())
