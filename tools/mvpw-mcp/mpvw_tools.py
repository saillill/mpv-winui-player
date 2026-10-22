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
import time
from ctypes import wintypes

import psutil
import uiautomation as auto

DEPLOY_DIR = r"C:\Users\z2637\Documents\DeepSeek\mpv-deploy"
EXE = os.path.join(DEPLOY_DIR, "mpvw.exe")
PIPE = r"\\.\pipe\mpvpipe"

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


def launch_app(wait_seconds: int = 20) -> dict:
    """Start mpvw.exe from the deploy directory and wait for its window."""
    existing = _pid()
    if existing:
        raise ToolError(f"mpvw.exe already running (pid {existing}); close it first")
    if not os.path.exists(EXE):
        raise ToolError(f"not found: {EXE}")

    proc = psutil.Popen([EXE], cwd=DEPLOY_DIR)
    deadline = time.time() + wait_seconds
    win = None
    while time.time() < deadline:
        time.sleep(0.5)
        win = _main_window(proc.pid, timeout=0.3)
        if win is not None:
            break
    if win is None:
        try:
            proc.kill()
        except Exception:
            pass
        raise ToolError(f"no window after {wait_seconds}s")

    time.sleep(1.5)  # first layout pass; the tree is incomplete before it
    return {"pid": proc.pid, "title": win.Name, "windowFound": True}


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

def _main_window(pid: int, timeout: float = 2.0):
    """The app's own top-level window, not whatever uid=1 search returns first.

    `WindowControl(searchDepth=1, ProcessId=pid)` looked right and once
    returned the Harness window instead of the player -- searchDepth is measured
    from the desktop root, so depth 1 is every top-level window and the pid
    filter is applied after. Enumerate the root's children and match the class
    the app actually uses.
    """
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            for w in auto.GetRootControl().GetChildren():
                try:
                    if w.ProcessId != pid:
                        continue
                    # The player page swaps the window's content but not its
                    # class; accept any visible top-level window of the process
                    # rather than requiring one specific class name, which made
                    # this return None while playing.
                    if not w.IsOffscreen:
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
                      rect: list[int] | None = None) -> dict:
    """Capture the window, one element, or [left, top, right, bottom] to a PNG."""
    from PIL import ImageGrab

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
    img = ImageGrab.grab(bbox=tuple(rect), all_screens=True)
    img.save(path)
    return {"path": os.path.abspath(path), "rect": rect,
            "width": img.width, "height": img.height}


# --------------------------------------------------------------------- mpv ---

_HANDLE = None
_READBUF = b""


def _open_pipe():
    """Open the mpv pipe once and keep it."""
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


def _ipc(command: list, request_id: int = 1, max_reads: int = 240):
    """One JSON IPC round trip over a held-open connection.

    mpv sends events and replies on the same stream, so a read returns whatever
    line is next. This keeps reading until the line whose request_id matches --
    the old version read ONE line and returned it, which handed callers the
    reply to a previous request (or an event).
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
        # mpv closes the pipe on requests it refuses to answer (a property that
        # does not exist under that name, one that is only valid with a file
        # loaded). Report it as an answer so a probe can continue.
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


def read_playback_state() -> dict:
    """Read the properties a status assertion needs.

    'property unavailable' is a valid answer (nothing loaded yet) and is
    reported as null rather than as an error.

    A pipe that answers nothing is NOT a valid answer. This used to return a dict
    of nulls in that case, and verify_e2e printed "pause=None volume=None
    idle=None" and called it PASS -- a checker reporting success while its
    instrument was dead, which is the exact failure mode AGENTS.md warns about.
    The dict now carries `ipcAvailable`, and callers must check it.
    """
    if _pid() is None:
        raise ToolError("mpvw.exe is not running")

    wanted = ["pause", "time-pos", "duration", "volume", "mute", "speed",
              "idle-active", "playlist-count", "filename", "media-title"]
    out = {"ipcAvailable": False, "unreachable": []}
    for rid, prop in enumerate(wanted, start=1):
        r = _ipc(["get_property", prop], rid)
        if r.get("noReply"):
            out[prop] = None
            out["unreachable"].append(prop)
            continue
        out[prop] = r.get("data") if r.get("error") == "success" else None
        if r.get("error") == "success":
            out["ipcAvailable"] = True
    if not out["ipcAvailable"]:
        raise ToolError(
            "the mpv IPC pipe accepted nothing (%d properties, all unanswered); "
            "the app may be alive with a dead pipe"
            % len(out["unreachable"]))
    return out


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

