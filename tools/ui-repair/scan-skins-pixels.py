"""Do the three skins differ? Geometric rects said no; compare window pixels instead.

The puzzle: `ApplyPlayerStyle` exists, the setting-change hook is subscribed, and both skin
styles set a `Template` -- so the player must change. Yet every control rect was identical
across Default / Center / Compact.

WinUI rects can be identical while the rendering differs (radius, brushes, button chrome
inside a template of the same bounds), so rects cannot answer this. Pixels can.

Capture method: `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=2)`. The older note in
AGENTS.md says PrintWindow misses WinUI popups -- true for Flyouts, which are separate
windows. The main window's own content composites normally, which is what is needed here.
CopyFromScreen would need the window on screen, and this task parks it off-screen so the
owner is not disturbed; PrintWindow does not need it visible or focused.

A capture that is nearly uniform is reported as unusable rather than compared: comparing
two blank images would "prove" the skins identical, which is the exact class of mistake
this repo has made repeatedly.
"""
import hashlib
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tools", "mvpw-mcp"))

import ctypes
import ctypes.wintypes as wt

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
SKINS = ("Default", "Center", "Compact")
REG = (r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui"
       r"\mpv-winui\app")

u32 = ctypes.WinDLL("user32", use_last_error=True)
g32 = ctypes.WinDLL("gdi32", use_last_error=True)


class BMI(ctypes.Structure):
    _fields_ = [("biSize", wt.DWORD), ("biWidth", ctypes.c_long),
                ("biHeight", ctypes.c_long), ("biPlanes", wt.WORD),
                ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
                ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", ctypes.c_long),
                ("biYPelsPerMeter", ctypes.c_long), ("biClrUsed", wt.DWORD),
                ("biClrImportant", wt.DWORD)]


def set_skin(name):
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    "Set-ItemProperty -Path '%s' -Name 'PlayerStyle' -Value '%s'"
                    % (REG, name)], capture_output=True)
    r = subprocess.run(["pwsh", "-NoProfile", "-Command",
                        "(Get-ItemProperty -Path '%s').PlayerStyle" % REG],
                       capture_output=True, text=True)
    return (r.stdout or "").strip()


def window_pixels():
    """(sha, bytes, note) for the app's main window, captured without showing it."""
    win = T._window()
    hwnd = win.NativeWindowHandle
    r = win.BoundingRectangle
    w, h = r.right - r.left, r.bottom - r.top
    if w <= 0 or h <= 0:
        return None, 0, "no size (%dx%d)" % (w, h)

    hdc = u32.GetWindowDC(hwnd)
    if not hdc:
        return None, 0, "GetWindowDC failed (%d)" % ctypes.get_last_error()
    try:
        mem = g32.CreateCompatibleDC(hdc)
        bmp = g32.CreateCompatibleBitmap(hdc, w, h)
        g32.SelectObject(mem, bmp)
        if not u32.PrintWindow(hwnd, mem, 2):      # PW_RENDERFULLCONTENT
            g32.DeleteObject(bmp)
            g32.DeleteDC(mem)
            return None, 0, "PrintWindow failed (%d)" % ctypes.get_last_error()

        bmi = BMI()
        bmi.biSize = ctypes.sizeof(BMI)
        bmi.biWidth = w
        bmi.biHeight = -h        # top-down
        bmi.biPlanes = 1
        bmi.biBitCount = 32
        bmi.biCompression = 0

        buf = ctypes.create_string_buffer(w * h * 4)
        got = g32.GetDIBits(mem, bmp, 0, h, buf, ctypes.byref(bmi), 0)
        g32.DeleteObject(bmp)
        g32.DeleteDC(mem)
        if not got:
            return None, 0, "GetDIBits returned 0"

        data = buf.raw
        # Distinct colours in the top band (where the control bar lives).
        band = data[:min(len(data), w * 4 * 160)]
        distinct = len({band[i:i + 4] for i in range(0, len(band) - 4, 4)})
        note = "ok" if distinct > 8 else "blank band (%d colours)" % distinct
        return hashlib.sha256(data).hexdigest()[:16], len(data), note
    finally:
        u32.ReleaseDC(hwnd, hdc)


def main():
    results = {}
    for skin in SKINS:
        stored = set_skin(skin)
        T.close_app()
        time.sleep(1.5)
        T.launch_app()
        try:
            T._ipc(["loadfile", CLIP, "replace"], 900)
        except Exception:
            pass
        time.sleep(3.0)
        h, n, note = window_pixels()
        results[skin] = (h, n, note)
        print("%-9s stored=%-9s hash=%-18s %s" % (skin, stored, h, note))
        T.close_app()

    usable = {s: v for s, v in results.items() if v[0] and v[2] == "ok"}
    print("\nusable captures: %d of %d" % (len(usable), len(SKINS)))
    if len(usable) < 2:
        print("INCONCLUSIVE -- not enough usable captures to compare.")
        for s, (h, n, note) in results.items():
            if note != "ok":
                print("   %-9s %s" % (s, note))
        return 2

    distinct = {v[0] for v in usable.values()}
    if len(distinct) < len(usable):
        print("\nVERDICT: at least two skins render identically.")
        print("  Setting stores, style hook exists, pixels match -> the skin is not")
        print("  reaching the player. That is a real defect.")
        return 1

    print("\nVERDICT: the skins render differently. Rect equality was the wrong")
    print("  instrument; there is no skin defect.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
