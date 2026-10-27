"""mpvw 的应用级 MCP 工具实现（共享逻辑层）。

这里只做「对 mpvw 做了什么」；MCP 协议封装在同目录的 mcpw_server.py。
每个函数返回 JSON 可序列化的 dict，失败时抛 ToolError（由 server 转成
MCP 的 isError 结果），不吞异常也不假装成功。

设计约束（全部来自本轮实测，不是假设）：
  * WinUI 3 的控件树 UIA 能读（`uiautomation` / UIA3），但导航项原本**没有
    AutomationId**（本轮已补 `ShellNav_<key>`），且元素 Name 常常是用户媒体
    库的标题 —— 所以 get_ui_snapshot 默认把 Name 脱敏，只给长度。
  * `uiautomation` 的 Rect 暴露 `left/top/right/bottom` 与 `width()/height()`
    **方法**；`Is*PatternAvailable` 属性不存在，pattern 一律走
    `GetPattern(PatternId.X)` 并用 `_pattern()` 做可用性判断。
  * 应用没有独立 mpv 窗口，播放状态最可靠的读法是 mpv 的 JSON IPC 命名管道
    `\\\\.\\pipe\\mpvpipe`（实测 get_property pause/volume 可用；未加载媒体时
    time-pos 返回 property unavailable —— 这是正常响应，不是错误）。
  * 启动必须从 mpv-deploy 目录，且不允许留下常驻进程。
"""
from __future__ import annotations

import ctypes
import json
import os
import subprocess
import time
from ctypes import wintypes

import psutil
import uiautomation as auto
import paths

DEPLOY_DIR = paths.deploy_dir()
EXE = os.path.join(DEPLOY_DIR, "mpvw.exe")
PIPE = r"\\.\pipe\mpvpipe"

# How a launched app is shown. TWO approaches were measured and both fail:
#
#   `Start-Process -WindowStyle Minimized`  does nothing. A WinUI 3 app creates its own
#       window and calls Activate(), which overrides the startup show state, so the
#       player still appeared over whatever the owner was doing.
#   ShowWindow(SW_MINIMIZE), or moving it off-screen
#       makes `_main_window` unable to find it, because that function requires
#       `not w.IsOffscreen` -- every later UIA call then raised "main window not found".
#
# What did NOT work, and why this does:
#
#   Start-Process -WindowStyle Minimized   no effect -- the app calls Activate(), which
#                                         overrides the startup show state, so the player
#                                         still appeared over whatever the owner was doing.
#   ShowWindow(SW_MINIMIZE)               still flashes on creation, and `_main_window`
#                                         could not find it.
#   SetWindowPos(HWND_BOTTOM) + focus     the owner still SAW it appear and still lost the
#     restore                             keyboard for a moment.
#   ShowWindow(SW_HIDE)                   UIA reports nothing; the probe surface disappears.
#   parking at (-32000,-32000)            removes the DWELL, but not the FLASH: the window
#                                         still spends the three to five seconds between
#                                         creation and parking on a monitor, fully active.
#
# That flash is what the user experiences as "verification keeps taking over my controller": a
# game in exclusive fullscreen drops its display mode and its controller the instant a window
# takes the foreground, and a 0.5 s foreground poll never sees it because it is over before the
# poll runs. The app does not open any gamepad device (measured: `gamepad`, `XInput`, `SDL2` and
# `hid.dll` all occur zero times in mpv-2.dll, mpvw.dll and mpv_winrt.dll, and no gamepad module
# is loaded at runtime), so the foreground flash is the entire mechanism.
#
# The fix is to stop the flash at its source: launch with MVPW_NO_ACTIVATE=1, which makes
# App.OnLaunched skip `window.Activate()`. The window is then created without ever taking the
# foreground, and parking it afterwards is only about keeping it off the screen.
#
# MVPW_FOREGROUND=1 still skips the parking for the cases that genuinely need pixels; with
# PrintWindow-based capture that is now almost nothing.
_START_FLAGS = ""
_PUSH_BACK = os.environ.get("MVPW_FOREGROUND") != "1"

# Passed on the launch line so the app never calls Activate(). Kept separate from _START_FLAGS
# because that one is about how PowerShell starts the process, not about the app's behaviour.
_NO_ACTIVATE_ENV = "MVPW_NO_ACTIVATE=1"

# One kernel32, with every signature declared once.
#
# ctypes defaults a foreign function's return type to c_long -- 32 bits here,
# while HANDLE is 64. CreateFileW then returns a TRUNCATED handle: a non-zero
# value that looks usable and addresses nothing. The symptom was a pipe that
# accepted exactly one request and then appeared dead, while a hand-written probe
# that set restype worked against the same process at the same moment.
_k32 = ctypes.WinDLL("kernel32", use_last_error=True)
_k32.CreateFileW.restype = wintypes.HANDLE
_k32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                             ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD,
                             wintypes.HANDLE]
_k32.CloseHandle.restype = wintypes.BOOL
_k32.CloseHandle.argtypes = [wintypes.HANDLE]
_k32.ReadFile.restype = wintypes.BOOL
_k32.ReadFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                          ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
_k32.WriteFile.restype = wintypes.BOOL
_k32.WriteFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                           ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]

# Patterns are obtained through this table; `auto.PatternId` is the public enum.
PATTERNS = {
    "invoke": auto.PatternId.InvokePattern,
    "select": auto.PatternId.SelectionItemPattern,
    "expand": auto.PatternId.ExpandCollapsePattern,
    "collapse": auto.PatternId.ExpandCollapsePattern,
    "toggle": auto.PatternId.TogglePattern,
    "value": auto.PatternId.ValuePattern,
    "range": auto.PatternId.RangeValuePattern,
    "scrollItem": auto.PatternId.ScrollItemPattern,
}


class ToolError(RuntimeError):
    pass


# ---------------------------------------------------------------- process ---

def _pid() -> int | None:
    for p in psutil.process_iter(["name", "pid"]):
        if (p.info["name"] or "").lower() == "mpvw.exe":
            return p.info["pid"]
    return None


def launch_app(wait_seconds: int = 20, snapshot: bool = False) -> dict:
    """Start mpvw.exe from the deploy directory and wait for its window.

    Launched through `Start-Process`, not `psutil.Popen`, and that is not
    cosmetic. Measured: the same exe started with psutil.Popen answers exactly
    one IPC request and then closes the pipe -- every later write fails with
    ERROR_NO_DATA (232) -- while the same exe started with Start-Process serves a
    long conversation. The app is identical; the launching context is not, and
    mpv's input-ipc-server is single-instance with default security.

    So this launches the way that works and finds the pid afterwards.
    """
    existing = _pid()
    if existing:
        raise ToolError(f"mpvw.exe already running (pid {existing}); close it first")
    if not os.path.exists(EXE):
        raise ToolError(f"not found: {EXE}")

    subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         f"$env:MVPW_NO_ACTIVATE='1'; Start-Process '{EXE}' -WorkingDirectory '{DEPLOY_DIR}'"
         + _START_FLAGS],
        capture_output=True)

    # Grab the IPC connection in the ~1.2 s window where mpv will accept one, and
    # read the status snapshot IMMEDIATELY -- both have to happen before the window
    # wait below.
    #
    # The measurement, from diag_ipc_matrix.py, is unambiguous:
    #     0.6 s  OK      1.1 s  OK      4.0 s  write 232
    # and launch-time matters more than launch method (psutil at 1.1 s also works).
    # A connection can also be made and then die while nothing is sent, so the read
    # is folded in here rather than left to a caller that runs UI steps first.
    global _STARTUP_STATE
    _STARTUP_STATE = None
    if snapshot:
        # Only read_playback_state asks for this, because the snapshot spends the
        # one connection this process will serve.
        #
        # The window is short (~1 s), and a fixed sleep gets it wrong whenever process
        # start-up drifts by a couple of hundred milliseconds: measured 0 of 3 attempts
        # with `time.sleep(1.1)`. So this polls from the instant the process appears
        # instead of guessing when it will be ready. A refused connection costs
        # microseconds and one accepted one is enough; the loop stops as soon as the
        # state is in hand.
        deadline = time.time() + 6.0
        while time.time() < deadline and _STARTUP_STATE is None:
            try:
                _open_pipe()
                _STARTUP_STATE = _read_state_on_open_connection()
            except ToolError:
                _close_pipe()
                time.sleep(0.05)
    else:
        # Nothing to open here, and that is the honest position.
        #
        # A tool that wants mpv status should call this with snapshot=True: the
        # snapshot is read inside the launch window, the only time a connection is
        # accepted. A tool that wants its own conversation cannot be helped from
        # here at all -- the window lasts about a second while waiting for the
        # window to exist takes three to five, so a handle opened now would be dead
        # before this function returns. Such a tool has to launch the app itself and
        # talk immediately; see diag_ipc_matrix.py for the shape that works.
        pass

    deadline = time.time() + wait_seconds
    proc_pid = None
    win = None
    while time.time() < deadline:
        time.sleep(0.3)
        if proc_pid is None:
            proc_pid = _pid()
            if proc_pid is None:
                continue
        win = _main_window(proc_pid, timeout=0.3)
        if win is not None:
            break
    if win is None:
        raise ToolError(f"no window after {wait_seconds}s")

    _push_to_back(win)
    time.sleep(1.5)  # first layout pass; the tree is incomplete before it
    return {"pid": proc_pid, "title": win.Name, "windowFound": True}


def _push_to_back(win):
    """Park the window outside every monitor so it never appears on the owner's screen.

    Called for every launch. Five approaches were measured before this one, and each
    previous one failed for its own reason:

      Start-Process -WindowStyle Minimized   no effect -- the app calls Activate().
      ShowWindow(SW_MINIMIZE)                still flashes on creation; and
                                             `_main_window` could not find it.
      SetWindowPos(HWND_BOTTOM) + focus      the owner still SAW it appear and still
        restore                              lost the keyboard for a moment. Putting a
                                             window behind others is not the same as
                                             not showing it, and this is the version
                                             that was called out twice as still
                                             stealing focus.
      ShowWindow(SW_HIDE)                    UIA reports nothing; the probe surface
                                             disappears.

    Parking at (-32000, -32000) is the one that works. Measured on this machine: the
    window reports `IsOffscreen = False`, 52 automation ids are readable including all
    five `ShellNav_*`, `invoke_control` succeeds, and the foreground window is
    unchanged. A WinUI layout does not depend on being on a monitor.

    MVPW_FOREGROUND=1 skips this for the cases that need real pixels: screenshots, and
    anything whose layout depends on the true screen size.
    """
    if not _PUSH_BACK:
        return
    try:
        u32 = ctypes.WinDLL("user32", use_last_error=True)
        u32.GetForegroundWindow.restype = wintypes.HWND
        u32.SetForegroundWindow.argtypes = [wintypes.HWND]
        u32.SetWindowPos.argtypes = [wintypes.HWND, wintypes.HWND,
                                     ctypes.c_int, ctypes.c_int, ctypes.c_int,
                                     ctypes.c_int, ctypes.c_uint]

        prev = u32.GetForegroundWindow()
        hwnd = win.NativeWindowHandle
        if not hwnd:
            return
        # Park outside every monitor: SWP_NOSIZE | SWP_NOACTIVATE at (-32000, -32000).
        u32.SetWindowPos(hwnd, wintypes.HWND(0), -32000, -32000, 0, 0,
                         0x0001 | 0x0010)
        if prev and prev != hwnd:
            u32.SetForegroundWindow(prev)
    except Exception:
        # Cosmetic only: a failure here must never fail a launch.
        pass


def open_launch_window(timeout: float = 4.0) -> dict:
    """Start the app and connect in the ~1 s window where mpv accepts a client.

    For tools that need their own conversation with mpv (verify_preview_api).
    launch_app() cannot serve them: it waits for the window, which takes 3-5 s,
    and by then a connection is refused outright.

    Returns the handle state so a caller can fail loudly if no connection was made
    rather than discover it one confusing request later.
    """
    _close_pipe()
    existing = _pid()
    if existing:
        raise ToolError(f"mpvw.exe already running (pid {existing}); close it first")
    if not os.path.exists(EXE):
        raise ToolError(f"not found: {EXE}")

    subprocess.run(
        ["pwsh", "-NoProfile", "-Command",
         f"$env:MVPW_NO_ACTIVATE='1'; Start-Process '{EXE}' -WorkingDirectory '{DEPLOY_DIR}'"
         + _START_FLAGS],
        capture_output=True)

    t0 = time.time()
    while time.time() - t0 < timeout:
        try:
            _open_pipe()
            return {"connected": True, "after": round(time.time() - t0, 2)}
        except ToolError:
            time.sleep(0.1)
    raise ToolError(f"no IPC connection within {timeout}s of launch")


def close_app(timeout: int = 15) -> dict:
    """Close the app the way the user would, so mpv flushes state.

    Returns only once the process is actually gone. `_pid()` scans psutil's
    process table, which is cached for a short while, so a single check right
    after the close reports the dead process as still alive -- that race made
    every later launch fail with "already running".
    """
    _close_pipe()
    pid = _pid()
    if pid is None:
        return {"closed": True, "alreadyClosed": True}
    try:
        win = _main_window(pid, timeout=1.0)
        if win is not None:
            win.GetWindowPattern().Close()
            # WinUI sometimes ignores the close while a modal popup is up, so the
            # wait below is not enough on its own; the kill path handles it.
    except Exception:
        pass

    # Ask the OS directly (not through psutil's cache) until the pid is gone.
    deadline = time.time() + timeout
    while time.time() < deadline:
        time.sleep(0.3)
        if not _pid_alive(pid):
            # psutil may still list it for one more tick; wait it out.
            for _ in range(20):
                if _pid() is None:
                    return {"closed": True, "forced": False}
                time.sleep(0.1)
            return {"closed": True, "forced": False, "psutilLag": True}

    try:
        psutil.Process(pid).kill()
    except Exception:
        pass
    for _ in range(40):
        if not _pid_alive(pid):
            break
        time.sleep(0.25)
    return {"closed": not _pid_alive(pid), "forced": True}


def _pid_alive(pid: int) -> bool:
    """True while the pid is a live process, asked of the OS each time."""
    try:
        h = ctypes.windll.kernel32.OpenProcess(0x1000, False, pid)  # QUERY_LIMITED
        if not h:
            return False
        code = wintypes.DWORD()
        ctypes.windll.kernel32.GetExitCodeProcess(h, ctypes.byref(code))
        ctypes.windll.kernel32.CloseHandle(h)
        return code.value == 259  # STILL_ACTIVE
    except Exception:
        return True



# --------------------------------------------------------------------- UIA ---

WINUI_WINDOW_CLASS = "WinUIDesktopWin32WindowClass"


def _main_window(pid: int, timeout: float = 2.0):
    """The app's own top-level window, not whatever search returns first.

    Two earlier versions, kept here because each fixed a real bug and the next change
    must not undo either:

      `WindowControl(searchDepth=1, ProcessId=pid)` returned the Harness window instead
      of the player -- searchDepth counts from the desktop root, so depth 1 is every
      top-level window and the pid filter is applied afterwards. Hence enumerating the
      root's children.

      "any visible window of the process" then allowed the popup/host windows the app
      creates. The class is now matched exactly (WINUI_WINDOW_CLASS), which is stricter
      than the visibility test it replaces rather than looser.

    Off-screen is accepted. The window is parked at (-32000, -32000) so it cannot appear
    on the owner's desktop (see _push_to_back), and `IsOffscreen` is False for such a
    window anyway -- measured -- so requiring `not IsOffscreen` was never what excluded
    it; requiring a *visible* window excluded nothing either. The class check is what
    keeps the wrong window out.

    Settings and PiP open additional windows of the same process. The player's main
    window is the one whose class matches AND which is not owned by another top-level
    window of the same process, so callers that need a specific one still work: the
    first match on the root is the main window in launch order.
    """
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            for w in auto.GetRootControl().GetChildren():
                try:
                    if w.ProcessId != pid:
                        continue
                    if (w.ClassName or "") == WINUI_WINDOW_CLASS:
                        return w
                except Exception:
                    continue
        except Exception:
            pass
        time.sleep(0.2)
    return None


def _window():
    pid = _pid()
    if pid is None:
        raise ToolError("mpvw.exe is not running")
    win = _main_window(pid, timeout=2.5)
    if win is None:
        raise ToolError("main window not found")
    return win


def _find(automation_id: str):
    win = _window()
    el = win.Control(searchDepth=16, AutomationId=automation_id)
    if not el.Exists(1, 0.2):
        raise ToolError(f"no element with AutomationId={automation_id!r}")
    return el


def _rect(el):
    """Rect -> [left, top, right, bottom]; None when the element never laid out.

    `uiautomation.Rect.width/height` are methods, not properties.
    """
    try:
        r = el.BoundingRectangle
        if r is None:
            return None
        left, top = int(r.left), int(r.top)
        right, bottom = int(r.right), int(r.bottom)
        if right <= left or bottom <= top:
            return None
        return [left, top, right, bottom]
    except Exception:
        return None


def _pattern(el, action: str):
    """Return the pattern object, or None when the element does not expose it."""
    pid_ = PATTERNS.get(action)
    if pid_ is None:
        return None
    try:
        return el.GetPattern(pid_)
    except Exception:
        return None


def get_ui_snapshot(max_depth: int = 6, redact_names: bool = True,
                    include_empty_ids: bool = False) -> dict:
    """Return the automation tree with ids, types, state and rectangles."""
    win = _window()
    rows = []

    def walk(ctrl, depth):
        if depth > max_depth:
            return
        try:
            aid = ctrl.AutomationId or ""
            name = ctrl.Name or ""
            ctype = ctrl.ControlTypeName
        except Exception:
            return
        if include_empty_ids or aid:
            rows.append({
                "depth": depth,
                "automationId": aid,
                "type": ctype,
                "nameLength": len(name),
                "name": "<redacted>" if (redact_names and name) else name,
                "enabled": bool(ctrl.IsEnabled) if ctrl.IsEnabled is not None else None,
                "rect": _rect(ctrl),
            })
        try:
            kids = ctrl.GetChildren()
        except Exception:
            return
        for k in kids:
            walk(k, depth + 1)

    walk(win, 0)
    return {"pid": _pid(), "windowTitle": win.Name, "count": len(rows),
            "elements": rows}


def invoke_control(automation_id: str, action: str = "invoke") -> dict:
    """Act on a control by AutomationId.

    action: invoke | select | expand | collapse | toggle | click
    Raises when the control does not expose the requested pattern, rather than
    reporting a success that did not happen.
    """
    el = _find(automation_id)

    if action == "click":
        if _rect(el) is None:
            raise ToolError(f"{automation_id} has no on-screen rectangle to click")
        el.Click()
        time.sleep(0.6)
        return {"automationId": automation_id, "action": "click"}

    pat = _pattern(el, action)
    if pat is None:
        raise ToolError(f"{automation_id} does not expose the {action} pattern; "
                        f"use action='click' if a mouse hit is acceptable")

    if action == "invoke":
        pat.Invoke()
    elif action == "select":
        pat.Select()
    elif action == "expand":
        pat.Expand()
    elif action == "collapse":
        pat.Collapse()
    elif action == "toggle":
        pat.Toggle()
    else:
        raise ToolError(f"unknown action {action!r}")

    time.sleep(0.6)
    return {"automationId": automation_id, "action": action}


def assert_element_state(automation_id: str, exists: bool = True,
                         enabled: bool | None = None,
                         visible: bool | None = None,
                         name_equals: str | None = None,
                         name_contains: str | None = None) -> dict:
    """Assert one control's state. Reports problems instead of throwing, so a
    test can collect several assertions before deciding."""
    problems = []
    try:
        el = _find(automation_id)
    except ToolError as e:
        if exists:
            return {"passed": False, "automationId": automation_id,
                    "problems": [str(e)]}
        return {"passed": True, "automationId": automation_id, "problems": []}

    if not exists:
        return {"passed": False, "automationId": automation_id,
                "problems": ["element exists, but exists=False was asserted"]}

    if enabled is not None and bool(el.IsEnabled) != enabled:
        problems.append(f"enabled is {el.IsEnabled}, expected {enabled}")

    if visible is not None:
        is_vis = _rect(el) is not None
        if is_vis != visible:
            problems.append(f"visible is {is_vis}, expected {visible}")

    cur = el.Name or ""
    if name_equals is not None and cur != name_equals:
        problems.append(f"name is {cur!r}, expected {name_equals!r}")
    if name_contains is not None and name_contains not in cur:
        problems.append(f"name {cur!r} does not contain {name_contains!r}")

    return {"passed": not problems, "automationId": automation_id,
            "problems": problems}


def screenshot_region(path: str, automation_id: str | None = None,
                      rect: list[int] | None = None,
                      method: str = "background") -> dict:
    """Capture the window, one element, or [left, top, right, bottom] to a PNG.

    `method="background"` (the default) renders the window with
    `PrintWindow(PW_RENDERFULLCONTENT)` into a bitmap this process owns, so **the window never
    has to be on a monitor**. That matters because the old behaviour -- `ImageGrab` of the
    desktop -- is what forced every screenshot probe to drag the window out of its parked
    position onto the screen, which the user experienced as "verification keeps jumping to the
    foreground and takes over an exclusive window".

    Measured on this machine with the window parked at (-32000,-32000): PrintWindow returned a
    full 2880x1536 image of the real UI (library grid, nav pane, search box, control bar), not
    blank, and `GetForegroundWindow` was unchanged throughout.

    `method="screen"` keeps the old desktop grab for the cases that need true composited pixels
    (GPU-composited video, popups drawn in a separate window). Those still require the window
    on-screen -- so they are opt-in and the result says which method produced it.
    """
    if rect is None:
        if automation_id:
            el = _find(automation_id)
            rect = _rect(el)
            if rect is None:
                raise ToolError(f"{automation_id} has no on-screen rectangle")
        else:
            win = _window()
            rect = _rect(win)
            if rect is None:
                raise ToolError("main window has no on-screen rectangle")

    os.makedirs(os.path.dirname(os.path.abspath(path)) or ".", exist_ok=True)

    if method == "screen":
        from PIL import ImageGrab
        img = ImageGrab.grab(bbox=tuple(rect), all_screens=True)
        img.save(path)
        return {"path": os.path.abspath(path), "rect": rect,
                "width": img.width, "height": img.height, "method": "ImageGrab(screen)"}

    # Background path: whole window only. A sub-rectangle is produced by cropping, because
    # PrintWindow renders the window, not an arbitrary screen region.
    import background_capture as _bgcap
    win = _window()
    hwnd = win.NativeWindowHandle
    full = _bgcap.capture_window(path, hwnd)
    full["rect"] = _rect(win)
    full["method"] = full.get("method", "PrintWindow")
    return full


# --------------------------------------------------------------------- mpv ---

_HANDLE = None
_READBUF = b""
# The status snapshot launch_app takes inside the one-second IPC window.
_STARTUP_STATE = None


def _open_pipe():
    """Open the mpv pipe and keep the handle.

    A connection is only usable while requests keep flowing. Measured: connect,
    then idle 2 s, then write -> ERROR_NO_DATA; the process is alive and UIA still
    works. mpv's input-ipc-server in this build accepts one client and closes it
    when the conversation pauses, so a long test must open a connection when it
    needs one rather than hold one across UI steps.
    """
    global _HANDLE, _READBUF
    GENERIC_READWRITE = 0xC0000000
    OPEN_EXISTING = 3
    k32 = _k32
    handle = k32.CreateFileW(PIPE, GENERIC_READWRITE, 0, None, OPEN_EXISTING, 0, None)
    if handle == wintypes.HANDLE(-1).value or handle is None:
        raise ToolError(f"cannot open {PIPE}: error {ctypes.get_last_error()}")
    _HANDLE = handle
    _READBUF = b""


def _close_pipe():
    global _HANDLE, _READBUF
    if _HANDLE is not None:
        try:
            _k32.CloseHandle(_HANDLE)
        except Exception:
            pass
    _HANDLE = None
    _READBUF = b""


def _pipe_worth_reopening() -> bool:
    """Whether mpv is still there to talk to (does not touch the pipe)."""
    return _pid() is not None


def _ipc(command: list, request_id: int = 1, max_reads: int = 240, retry: bool = True):
    """One JSON IPC round trip on the connection this process will serve.

    mpv sends events and replies on the same stream, so a read returns whatever
    line is next; this keeps reading until the line whose request_id matches.

    A connection made later than about a second after launch is refused
    (ERROR_NO_DATA, 232), so callers do not get to decide when to connect: the
    first _ipc call opens it, which means a tool that calls launch_app() and then
    _ipc() immediately is fine, while one that runs UI steps first is not. That is
    the real constraint in this build, measured in diag_ipc_matrix.py.
    """
    global _HANDLE, _READBUF
    if _HANDLE is None:
        _open_pipe()

    k32 = _k32
    payload = (json.dumps({"command": command, "request_id": request_id}) + "\n").encode()
    written = wintypes.DWORD(0)
    if not k32.WriteFile(_HANDLE, payload, len(payload), ctypes.byref(written), None):
        err = ctypes.get_last_error()
        _close_pipe()
        # A dead connection and a refused command look the same here. One retry on
        # a fresh connection, and only while the process is alive, separates them:
        # a refused command fails again and is reported, a stale connection is
        # repaired.
        if retry and err in (232, 109) and _pipe_worth_reopening():
            return _ipc(command, request_id, max_reads, retry=False)
        return {"request_id": request_id, "error": "no reply (write failed)",
                "win32Error": err, "noReply": True}

    buf = ctypes.create_string_buffer(65536)
    read = wintypes.DWORD(0)
    for _ in range(max_reads):
        while b"\n" in _READBUF:
            line, _READBUF = _READBUF.split(b"\n", 1)
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
            except Exception:
                continue
            if obj.get("request_id") == request_id:
                return obj
        if not k32.ReadFile(_HANDLE, buf, 65536, ctypes.byref(read), None):
            err = ctypes.get_last_error()
            _close_pipe()
            return {"request_id": request_id, "error": "no reply (the pipe closed)",
                    "win32Error": err, "noReply": True}
        if read.value:
            _READBUF += buf.raw[: read.value]
        else:
            time.sleep(0.01)
    return {"request_id": request_id, "error": "no reply (timeout)", "noReply": True}


def _read_state_on_open_connection() -> dict:
    """The status properties, read on the connection that is already open."""
    wanted = ["pause", "time-pos", "duration", "volume", "mute", "speed",
              "idle-active", "playlist-count", "filename", "media-title"]
    out = {"ipcAvailable": False, "unreachable": [], "readAt": "launch"}
    for rid, prop in enumerate(wanted, start=1):
        r = _ipc(["get_property", prop], rid, retry=False)
        if r.get("noReply"):
            out[prop] = None
            out["unreachable"].append(prop)
            continue
        out[prop] = r.get("data") if r.get("error") == "success" else None
        if r.get("error") == "success":
            out["ipcAvailable"] = True
    return out


def read_playback_state() -> dict:
    """The status properties, from the connection launch_app opened.

    Why this is not a fresh read: in this build mpv's input-ipc-server accepts a
    client only in roughly the first second after launch. Measured
    (diag_ipc_matrix.py): 0.6 s and 1.1 s work, 4.0 s fails with ERROR_NO_DATA,
    and a connection that is made and then left idle dies too. So launch_app opens
    the pipe in that window and reads the snapshot immediately; this function
    returns that snapshot.

    'property unavailable' is a valid answer (nothing loaded yet) and is reported
    as null. A pipe that answered nothing is NOT valid -- an earlier version
    returned a dict of nulls and verify_e2e called that PASS, which is why this
    raises instead.
    """
    if _pid() is None:
        raise ToolError("mpvw.exe is not running")

    if _STARTUP_STATE is None:
        raise ToolError(
            "no mpv status snapshot: the pipe accepts a client only in the first "
            "second or two after launch, and launch_app could not open it then")

    if not _STARTUP_STATE.get("ipcAvailable"):
        raise ToolError(
            "the mpv IPC pipe accepted nothing (%d properties, all unanswered); "
            "the app may be alive with a dead pipe"
            % len(_STARTUP_STATE.get("unreachable", [])))
    return _STARTUP_STATE


def trigger_screenshot(fmt: str = "png", path: str | None = None,
                       mode: str = "subtitles") -> dict:
    """Ask mpv itself to write a screenshot.

    mpv's command is `screenshot-to-file <path> <flags>`, and the third argument
    is *not* the image format -- it is the screenshot mode flags (`subtitles`,
    `video`, or both). Passing "png" there returns `invalid parameter`, which is
    what the first version of this tool did. The format comes from the
    `screenshot-format` property, so this sets that first, then calls the
    command with the mode. The app does the same at
    MpvPlayerPage_PlaylistPanel.cs:221.

    fmt:  png | jpg | webp | jxl | avif, whatever the bundled mpv can write
    mode: subtitles (default, what the app's menu uses) | video | subtitles+video
    """
    if fmt not in ("png", "jpg", "webp", "jxl", "avif"):
        raise ToolError(f"unsupported format {fmt!r}")

    # screenshot-format must be set before the command runs, and it is a
    # property, not an argument.
    fmt_reply = _ipc(["set_property", "screenshot-format", fmt], 800)

    if not path:
        stamp = time.strftime("%Y%m%d-%H%M%S")
        base = os.path.join(DEPLOY_DIR, "screenshots")
        os.makedirs(base, exist_ok=True)
        path = os.path.join(base, f"mpvw-{stamp}.{fmt}")

    r = _ipc(["screenshot-to-file", path, mode], 801)
    return {"requested": path, "format": fmt, "mode": mode,
            "formatReply": fmt_reply, "reply": r,
            "written": os.path.exists(path) and os.path.getsize(path) > 0}

