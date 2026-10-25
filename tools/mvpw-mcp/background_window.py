"""Background-safe window placement for probes: move, read, put back, never steal focus.

**Why this exists.** The user reported that verification "keeps jumping to the foreground and
takes over an exclusive window". Meeting it, not one product path is responsible:

  measured   launch/close cycles leave the foreground window alone (3/3, twice);
  measured   opening the settings window leaves it alone, even with the main window moved
             on-screen -- because Windows refuses `SetForegroundWindow` from a process that does
             not own the foreground, which is every probe;
  found      the thief is the PROBES. Seven of them call
             `SetWindowPos(..., SWP_SHOWWINDOW)` to drag the window from its parked position at
             (-32000,-32000) onto the screen, so they can screenshot it.

The product does contain focus-taking code -- `App.OnActivated -> Window.ShowWindow() ->
SetForegroundWindow`, and `WindowsManager.Open -> window.Activate()` for every child window --
and that code becomes live exactly when the app already owns the foreground, i.e. while the user
is using it. That is a separate question and is tracked in QUEUE; it is NOT what makes a
verification run jump, and fixing it would not stop the jumping.

What this module gives a probe instead of `SWP_SHOWWINDOW`:

    with OnScreen(hwnd) as rect:      # moves in with NOACTIVATE, yields the settled rect
        ...sample pixels...

and on exit the window goes back to exactly where it was. `SWP_NOACTIVATE` is the part that
matters: the window becomes visible and paintable without becoming active or taking the
keyboard, so `GetForegroundWindow()` is unchanged and nothing is covered.

Also provided: `foreground_guard()` as a context manager that records the foreground HWND on
entry and, on exit, ASSERTS it did not change -- so a probe that accidentally activates the app
fails loudly instead of silently annoying the user. That assertion is the part that turns "we
tried to be polite" into a checkable property.
"""
import contextlib
import ctypes
import ctypes.wintypes as wt
import time

u32 = ctypes.WinDLL("user32", use_last_error=True)
u32.GetForegroundWindow.restype = wt.HWND

SWP_NOZORDER = 0x0004
SWP_NOACTIVATE = 0x0010

# Park position the harness uses; matches mpvw_tools.py.
PARKED = -32000


class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                ("right", ctypes.c_long), ("bottom", ctypes.c_long)]


def get_rect(hwnd):
    r = RECT()
    u32.GetWindowRect(wt.HWND(hwnd), ctypes.byref(r))
    return (r.left, r.top, r.right, r.bottom)


def foreground():
    return int(u32.GetForegroundWindow() or 0)


@contextlib.contextmanager
def foreground_guard(reader=None):
    """Fail the probe if the foreground window changed while the block ran.

    Yields a dict that the caller can add notes to; `changed` is filled on exit.

    `reader` exists **only so this decision can be tested**, and it is needed because of a
    measured limit: from a probe process nothing can move the real foreground window here.
    `SetForegroundWindow`, the same after `AttachThreadInput`, after
    `AllowSetForegroundWindow`, and `SwitchToThisWindow` all leave `GetForegroundWindow`
    unchanged, because the process does not own the foreground and Windows enforces that. So
    the raising branch cannot be reached by moving the real foreground from a probe, and a
    checker whose failure branch has never run is exactly the trap this repo has been burned
    by more than once.

    Production callers pass nothing and get `foreground`, so behaviour is unchanged.
    `_work/probe_guard_validation.py` passes a scripted reader and asserts that a change
    raises and a constant reading does not.
    """
    read = reader or foreground
    before = read()
    info = {"before": before, "after": None, "changed": None}
    try:
        yield info
    finally:
        after = read()
        info["after"] = after
        info["changed"] = (after != before)
        if info["changed"]:
            raise AssertionError(
                "probe stole the foreground window: %s -> %s" % (before, after))


@contextlib.contextmanager
def OnScreen(hwnd, width=None, height=None, x=20, y=20, settle=8.0):
    """Move a window on-screen WITHOUT activating it, then restore its exact position.

    The move uses SWP_NOACTIVATE, so the window appears without taking focus or the
    keyboard. The original rectangle is captured first and restored on exit, so a probe
    leaves the app parked where the harness put it.
    """
    original = get_rect(hwnd)
    w = width if width is not None else (original[2] - original[0])
    h = height if height is not None else (original[3] - original[1])

    u32.SetWindowPos(wt.HWND(hwnd), None, x, y, w, h,
                     SWP_NOZORDER | SWP_NOACTIVATE)

    # Wait for the rect to stop changing rather than sleeping a guessed duration.
    last = None
    t0 = time.time()
    settled = None
    while time.time() - t0 < settle:
        time.sleep(0.15)
        cur = get_rect(hwnd)
        if cur == last:
            settled = cur
            break
        last = cur

    try:
        yield settled or get_rect(hwnd)
    finally:
        u32.SetWindowPos(wt.HWND(hwnd), None,
                         original[0], original[1],
                         original[2] - original[0], original[3] - original[1],
                         SWP_NOZORDER | SWP_NOACTIVATE)


def is_parked(hwnd, tolerance=1000):
    r = get_rect(hwnd)
    return abs(r[0] - PARKED) < tolerance and abs(r[1] - PARKED) < tolerance
