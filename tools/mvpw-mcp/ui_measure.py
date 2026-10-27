"""Measure the four UI properties the 9 open defects are about, instead of eyeballing them.

Every one of the nine queued UI defects is a claim of one of these forms:

    "this button is in the wrong place"        -> alignment
    "the icon is partly hidden"                -> obscuring
    "the cover image does not show"            -> image content
    "the videos are expanded instead of in columns" -> layout/columns
    "the letter rail shows odd overlapping buttons" -> it should be TEXT only

A screenshot can show all of these and still be argued about, which is why screenshots are
not evidence of behaviour here. These four functions turn each claim into a number with a
threshold, so a fix is a before/after pair rather than a second opinion.

All four take live `uiautomation` controls, so they run against the deployed app the same
way the other verifiers do. Nothing here launches or closes the app: callers own that.

Measured facts this relies on (from AGENTS.md, re-checked on this machine):
  * UIA BoundingRectangle is in physical pixels at the window's DPI scale (x1.5 here), so
    comparing raw rectangles is safe, and comparing against XAML DIPs is NOT -- hence
    everything below reports in whatever unit the rectangles arrive in and compares only
    rectangles to each other.
  * `Grid` is not a UIA node. A container you can see may have no element, so callers must
    target something that does have one; `element_from_point` still works at any pixel.
"""
from __future__ import annotations

import ctypes
from ctypes import wintypes

import uiautomation as auto

user32 = ctypes.WinDLL("user32", use_last_error=True)


# ---------------------------------------------------------------------------
# geometry primitives


def rect_of(control) -> tuple[int, int, int, int] | None:
    """(left, top, right, bottom) in physical pixels, or None if not on screen."""
    try:
        r = control.BoundingRectangle
    except Exception:
        return None
    if r is None:
        return None
    box = (r.left, r.top, r.right, r.bottom)
    if box[2] <= box[0] or box[3] <= box[1]:
        return None
    return box


def _centre(box):
    return ((box[0] + box[2]) // 2, (box[1] + box[3]) // 2)


def _describe(control) -> str:
    try:
        name = control.Name or ""
    except Exception:
        name = ""
    try:
        aid = control.AutomationId or ""
    except Exception:
        aid = ""
    kind = type(control).__name__
    label = aid or name or kind
    return "%s(%s)" % (kind, label) if (aid or name) else kind


# ---------------------------------------------------------------------------
# 1. alignment


def assert_alignment(a, b, tolerance_px: int = 3) -> dict:
    """Two controls are aligned when their edges agree within `tolerance_px`.

    The tolerance exists because UIA rectangles are physical pixels and a 2 DIP difference
    is 3 px at x1.5. Passing 0 would fail on rounding rather than on layout.

    Reports every axis independently instead of one boolean, because "these two are not
    aligned" is useless: which edge differs, and by how much, is the whole answer. The
    common real case is that the left edges match but the heights do not, which means the
    defect is padding rather than position.
    """
    ra, rb = rect_of(a), rect_of(b)
    if ra is None or rb is None:
        return {"ok": False, "reason": "one of the controls is not on screen",
                "a": _describe(a), "b": _describe(b), "a_rect": ra, "b_rect": rb}

    deltas = {
        "left": ra[0] - rb[0],
        "top": ra[1] - rb[1],
        "right": ra[2] - rb[2],
        "bottom": ra[3] - rb[3],
        "height": (ra[3] - ra[1]) - (rb[3] - rb[1]),
    }
    off = {k: v for k, v in deltas.items() if abs(v) > tolerance_px}
    return {
        "ok": not off,
        "a": _describe(a),
        "b": _describe(b),
        "deltas": deltas,
        "off_by": off,
        "tolerance_px": tolerance_px,
    }


# ---------------------------------------------------------------------------
# 2. obscuring


def assert_not_obscured(control, samples: int = 9) -> dict:
    """Is the control actually the thing at its own pixels?

    `ElementFromPoint` asks the automation tree what is on top at a given screen pixel --
    the same question a user's click asks. A control that is visually behind a flyout, a
    gradient overlay or another panel still reports a perfectly normal BoundingRectangle,
    which is exactly why "the icon is hidden" cannot be tested by reading properties.

    Samples a 3x3 grid rather than the centre only: the real defect here is an icon whose
    centre is visible while an overlay covers its top or bottom edge, and a centre-only
    probe reports that as fine.
    """
    box = rect_of(control)
    if box is None:
        return {"ok": False, "reason": "not on screen", "control": _describe(control)}

    # WinUI 3 answers ControlFromPoint with the top-most element, usually a PaneControl
    # WRAPPER rather than the Button.
    #
    # Two corrections, both forced by measurement:
    #
    #  1. Comparing with `is` reported EVERY control on the player bar as covered, including
    #     plainly visible ones -- a measurement that cannot distinguish a hidden icon from a
    #     visible one.
    #  2. Comparing ancestors by `id()` was not reliable either: uiautomation hands back a new
    #     Python wrapper per call, so the same element has a different id. Measured on
    #     PlayPauseButton: the covering PaneControl was the app's own content container
    #     (rect 144,133,3000,1656 -- the whole content area, same process) and the id-based
    #     ancestor set did not contain it.
    #
    # The reliable identity for "is this covering element one of the control's containers"
    # is geometric: a container's rectangle CONTAINS the control's. Something genuinely on
    # top either sits inside the control's own rectangle (its glyph, its text) or comes from
    # another process (a window in front).
    def related(other) -> bool:
        """True when `other` is the control, one of its containers, or inside its rectangle."""
        if other is control:
            return True
        orb = rect_of(other)
        if orb is None:
            return True  # unmeasurable: do not accuse it of covering
        # Inside the control's own rectangle: its glyph, label, or a child.
        if orb[0] >= box[0] and orb[1] >= box[1] and orb[2] <= box[2] and orb[3] <= box[3]:
            return True
        # A container: its rectangle contains the control's.
        if orb[0] <= box[0] and orb[1] <= box[1] and orb[2] >= box[2] and orb[3] >= box[3]:
            return True
        return False

    # A window from ANOTHER process covering every sample is a broken measurement, not a UI
    # defect. Measured: with a browser maximised over the app, all nine samples of
    # PlayPauseButton came back as `GroupControl (0,0,3842,2078)` -- the browser -- and the
    # probe reported the button as hidden while it was perfectly visible behind it. The
    # distinguishing question is whether the covering element belongs to the app at all.
    try:
        app_pid = control.ProcessId
    except Exception:
        app_pid = None

    hits, obscured = [], []
    foreign = 0
    for i in range(samples):
        fx = 0.2 + 0.6 * ((i % 3) / 2.0)
        fy = 0.2 + 0.6 * ((i // 3) / 2.0)
        x = int(box[0] + (box[2] - box[0]) * fx)
        y = int(box[1] + (box[3] - box[1]) * fy)
        try:
            top = auto.ControlFromPoint(x, y)
        except Exception as e:
            hits.append({"x": x, "y": y, "error": type(e).__name__})
            continue
        if top is None:
            hits.append({"x": x, "y": y, "top": None})
            continue
        ok = related(top)
        try:
            top_pid = top.ProcessId
        except Exception:
            top_pid = None
        if not ok and app_pid is not None and top_pid is not None and top_pid != app_pid:
            foreign += 1
        entry = {"x": x, "y": y, "top": _describe(top), "related": ok}
        hits.append(entry)
        if not ok:
            obscured.append(entry)

    if foreign and foreign == len([h for h in hits if "top" in h]):
        return {
            "ok": False,
            "measurement_invalid": True,
            "reason": ("every sample was covered by a window from another process -- the app "
                       "is behind something. Bring it to the front and re-measure; this is not "
                       "a UI defect."),
            "control": _describe(control),
            "control_rect": box,
            "samples": hits,
            "covered_by": obscured,
        }

    return {
        "ok": not obscured,
        "control": _describe(control),
        "control_rect": box,
        "samples": hits,
        "covered_by": obscured,
    }


# ---------------------------------------------------------------------------
# 3. image content


def assert_image_content(control, min_stddev: float = 12.0,
                         min_colours: int = 24) -> dict:
    """Does the region contain a picture, or just a flat fill?

    The queued defect is "the cover shows as a long grey bar", i.e. a layout that is
    correct and an image that never arrived. Geometry cannot see that; a screenshot can,
    and two numbers separate the cases without an opinion:

      standard deviation  -- a flat grey fill is ~0, a real cover is much higher
      distinct colours    -- a placeholder is 1-3 colours, artwork is many

    Both thresholds are deliberately low: this is meant to catch "nothing was drawn", not
    to judge whether the artwork is any good. A dark cover with a narrow palette still
    passes; an empty Border does not.
    """
    from PIL import ImageGrab
    import statistics

    box = rect_of(control)
    if box is None:
        return {"ok": False, "reason": "not on screen", "control": _describe(control)}

    img = ImageGrab.grab(bbox=box, all_screens=True).convert("RGB")
    # Shrink before counting so a photograph's noise cannot stand in for content, and so
    # a 2000-px region costs the same as a 200-px one.
    small = img.resize((48, 48))
    pixels = list(small.getdata())
    grey = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b in pixels]
    try:
        sd = statistics.pstdev(grey)
    except statistics.StatisticsError:
        sd = 0.0
    colours = len(set(pixels))

    return {
        "ok": sd >= min_stddev and colours >= min_colours,
        "control": _describe(control),
        "control_rect": box,
        "stddev": round(sd, 2),
        "distinct_colours": colours,
        "min_stddev": min_stddev,
        "min_colours": min_colours,
        "verdict": ("has image content"
                    if sd >= min_stddev and colours >= min_colours
                    else "looks like a flat fill, not a picture"),
    }


# ---------------------------------------------------------------------------
# 4. columns


def assert_image_content_in(box, min_stddev: float = 12.0,
                           min_colours: int = 24) -> dict:
    """assert_image_content for a rectangle rather than a control.

    Needed because the cover is not the whole card. Measuring the card is what made a
    coverless music card pass: the media-kind glyph and the two captions supplied enough
    variance to clear the threshold, so the queued 音乐库的封面依旧没有加载出来 was reported
    against a verifier that had just printed PASS. The cover occupies the top square (or the
    top 16:9 band on a song card), so that band is what has to be measured.

    ox is (left, top, right, bottom) in screen pixels, the same shape rect_of returns.
    """
    from PIL import ImageGrab
    import statistics

    if box is None:
        return {"ok": False, "reason": "no box"}

    try:
        img = ImageGrab.grab(bbox=box, all_screens=True).convert("RGB")
    except Exception as e:
        return {"ok": False, "reason": "capture failed: %s" % e}

    if img.size[0] <= 0 or img.size[1] <= 0:
        return {"ok": False, "reason": "empty box %s" % (box,)}

    small = img.resize((48, 48))
    pixels = list(small.getdata())
    grey = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b in pixels]
    try:
        sd = statistics.pstdev(grey)
    except statistics.StatisticsError:
        sd = 0.0
    colours = len(set(pixels))

    return {
        "ok": sd >= min_stddev and colours >= min_colours,
        "stddev": round(sd, 1),
        "distinct_colours": colours,
        "box": box,
    }


def assert_columns(controls, min_columns: int = 2, tolerance_px: int = 8) -> dict:
    """Are these laid out in N columns, or all in one row/one column?

    The queued defect is "the video folder should show a large path and a grid of folder
    cards, not every video expanded in one column". Counting is enough to tell those apart,
    and it is robust: a grid of cards shares a small set of distinct left edges, one column
    has exactly one.

    Groups by left edge rather than by index, so a grid that is ordered differently still
    measures as a grid.
    """
    boxes = [(c, rect_of(c)) for c in controls]
    boxes = [(c, b) for c, b in boxes if b is not None]
    if len(boxes) < min_columns:
        return {"ok": False, "reason": "fewer visible controls than columns requested",
                "visible": len(boxes), "min_columns": min_columns}

    lefts = sorted(b[0] for _, b in boxes)
    groups = [[lefts[0]]]
    for x in lefts[1:]:
        if x - groups[-1][-1] <= tolerance_px:
            groups[-1].append(x)
        else:
            groups.append([x])

    rows = sorted(b[1] for _, b in boxes)
    row_groups = [[rows[0]]]
    for y in rows[1:]:
        if y - row_groups[-1][-1] <= tolerance_px:
            row_groups[-1].append(y)
        else:
            row_groups.append([y])

    return {
        "ok": len(groups) >= min_columns,
        "visible": len(boxes),
        "columns": len(groups),
        "rows": len(row_groups),
        "min_columns": min_columns,
        "column_lefts": [g[0] for g in groups],
        "row_tops": [g[0] for g in row_groups],
    }


# ---------------------------------------------------------------------------
# placement predicates
#
# These are plain functions over rectangles rather than assertions over controls, on purpose:
# an alignment claim is about geometry, and geometry can be checked against numbers taken from
# a real measurement without launching the app.


def is_near_right_edge(rect: tuple[int, int, int, int],
                       window: tuple[int, int, int, int],
                       inset_px: int = 200) -> bool:
    """True when the control's right edge is within `inset_px` of the window's.

    This is what separates "there is a button" from "the button is where the report says it
    should be". A control against the LEFT gutter in a 3000-wide window fails by a mile, which
    is what makes it a useful check rather than a restatement of presence.
    """
    if rect is None or window is None:
        return False
    return rect[2] >= window[2] - inset_px


def same_row(a: tuple[int, int, int, int],
             b: tuple[int, int, int, int],
             tolerance_px: int = 30) -> bool:
    """True when two controls' vertical centres are within `tolerance_px`.

    Centres rather than tops, because two controls in one row can differ in height (a pill
    next to a bare glyph button) while still reading as one line.
    """
    if a is None or b is None:
        return False
    return abs((a[1] + a[3]) / 2 - (b[1] + b[3]) / 2) <= tolerance_px


# ---------------------------------------------------------------------------
# reporting


def report(label: str, result: dict) -> bool:
    """Print one measurement as a single machine-readable line plus its detail."""
    ok = bool(result.get("ok"))
    print("%-5s %-46s %s" % ("OK" if ok else "FAIL", label,
                             _summarise(result)))
    for k, v in result.items():
        if k == "ok":
            continue
        if isinstance(v, (list, dict)) and len(str(v)) > 160:
            print("        %s: %s ..." % (k, str(v)[:160]))
        else:
            print("        %s: %s" % (k, v))
    return ok


def _summarise(result: dict) -> str:
    if "off_by" in result:
        return "off_by=%s" % (result["off_by"] or "nothing")
    if "covered_by" in result:
        n = len(result["covered_by"])
        return "covered at %d sample(s)" % n if n else "not covered"
    if "stddev" in result:
        return "stddev=%.1f colours=%d" % (result["stddev"], result["distinct_colours"])
    if "columns" in result:
        return "%d columns x %d rows over %d items" % (
            result["columns"], result["rows"], result["visible"])
    return result.get("reason", "")
