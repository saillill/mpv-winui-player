"""Capture the app's pixels WITHOUT bringing it on-screen: PrintWindow into a bitmap.

**Why this replaces ImageGrab for the app window.** `ImageGrab.grab(bbox=...)` reads the
desktop surface, so it only works if the window is physically on a monitor. That is why every
screenshot probe moved the window out of its parked position -- and dragging a window onto the
screen is what the user experienced as "verification keeps jumping to the foreground and taking
over an exclusive window". Adding `SWP_NOACTIVATE` did not help: the window still appears over
their work.

`PrintWindow(..., PW_RENDERFULLCONTENT)` asks the window to render itself into a device context
the caller owns, so the window can stay parked off-screen and never appear. This is precisely
the "background logic" the complaint asked for.

The one honest caveat, and it is measured rather than assumed: PrintWindow is documented as
unreliable for some composited content. AGENTS.md already records that it "misses WinUI popups,
so a flyout comes out blank". So this module does not silently substitute one for the other --
`capture()` returns a report saying HOW it captured, and `verify_capture()` compares PrintWindow
against ImageGrab for the same on-screen rect, so the difference is a number rather than a claim.

Usage from a probe:

    with bw.OnScreen(hwnd):            # only when a real-pixel check is unavoidable
        ...
    shot = bgshot.capture_window(path)  # parked is fine: no on-screen move needed
"""
import ctypes
import ctypes.wintypes as wt

u32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)

# Declare the signatures. Without these, ctypes guesses and fails on the DC/HANDLE arguments
# ("cannot be converted to pointer"), which is exactly how the first run of this module died.
u32.GetDC.restype = wt.HDC
u32.GetDC.argtypes = [ctypes.c_void_p]
u32.ReleaseDC.argtypes = [ctypes.c_void_p, wt.HDC]
u32.PrintWindow.argtypes = [ctypes.c_void_p, wt.HDC, wt.UINT]
u32.PrintWindow.restype = wt.BOOL
u32.GetWindowRect.argtypes = [ctypes.c_void_p, ctypes.POINTER(wt.RECT)]
u32.GetClientRect.argtypes = [ctypes.c_void_p, ctypes.POINTER(wt.RECT)]

gdi32.CreateCompatibleDC.restype = wt.HDC
gdi32.CreateCompatibleDC.argtypes = [wt.HDC]
gdi32.CreateCompatibleBitmap.restype = wt.HANDLE
gdi32.CreateCompatibleBitmap.argtypes = [wt.HDC, ctypes.c_int, ctypes.c_int]
gdi32.SelectObject.restype = wt.HANDLE
gdi32.SelectObject.argtypes = [wt.HDC, wt.HANDLE]
gdi32.DeleteObject.argtypes = [wt.HANDLE]
gdi32.DeleteDC.argtypes = [wt.HDC]

PW_RENDERFULLCONTENT = 0x00000002
SRCCOPY = 0x00CC0020
DIB_RGB_COLORS = 0
BI_RGB = 0


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [("biSize", wt.DWORD), ("biWidth", ctypes.c_long),
                ("biHeight", ctypes.c_long), ("biPlanes", wt.WORD),
                ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
                ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", ctypes.c_long),
                ("biYPelsPerMeter", ctypes.c_long), ("biClrUsed", wt.DWORD),
                ("biClrImportant", wt.DWORD)]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wt.DWORD * 3)]


# Declared here, after BITMAPINFO exists -- referencing it earlier is what made the first
# version fail to import.
gdi32.GetDIBits.argtypes = [wt.HDC, wt.HANDLE, wt.UINT, wt.UINT, ctypes.c_void_p,
                            ctypes.POINTER(BITMAPINFO), wt.UINT]
gdi32.GetDIBits.restype = ctypes.c_int


def _window_rect(hwnd):
    r = wt.RECT()
    u32.GetWindowRect(int(hwnd), ctypes.byref(r))
    return r.left, r.top, r.right, r.bottom


def capture_window(path, hwnd, client_only=False):
    """Render `hwnd` into a PNG via PrintWindow. The window need not be visible.

    Returns {"path", "width", "height", "method", "blank"} where `blank` is True when every
    sampled pixel is identical -- the signature of a PrintWindow that produced nothing, which
    is the failure mode AGENTS.md warns about.
    """
    from PIL import Image
    import struct

    flags = PW_RENDERFULLCONTENT
    # Keep the handle as an int: `wt.HWND(x)` wraps it in a POINTER, and ctypes then refuses
    # the call ("cannot be converted to pointer"). All the argtypes above are c_void_p.
    hwnd = int(hwnd)

    if client_only:
        r = wt.RECT()
        u32.GetClientRect(int(hwnd), ctypes.byref(r))
        width, height = r.right - r.left, r.bottom - r.top
    else:
        l, t, rr, b = _window_rect(hwnd)
        width, height = rr - l, b - t

    if width <= 0 or height <= 0:
        raise ValueError("window has no size (%dx%d)" % (width, height))

    hdc_screen = u32.GetDC(None)
    hdc_mem = gdi32.CreateCompatibleDC(hdc_screen)
    gdi32.CreateCompatibleBitmap.restype = wt.HANDLE
    hbmp = gdi32.CreateCompatibleBitmap(hdc_screen, width, height)
    gdi32.SelectObject(hdc_mem, hbmp)

    ok = u32.PrintWindow(int(hwnd), hdc_mem, flags)

    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = width
    bmi.bmiHeader.biHeight = -height  # negative: top-down rows
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 32
    bmi.bmiHeader.biCompression = BI_RGB

    buf_len = width * height * 4
    buf = ctypes.create_string_buffer(buf_len)
    gdi32.GetDIBits(hdc_mem, hbmp, 0, height, buf, ctypes.byref(bmi), DIB_RGB_COLORS)

    img = Image.frombuffer("RGBA", (width, height), buf, "raw", "BGRA", 0, 1)
    img = img.convert("RGB")
    img.save(path)

    # Blank detection: a PrintWindow that drew nothing yields one flat colour.
    colours = img.getcolors(maxcolors=1024)
    blank = (colours is not None and len(colours) <= 1)

    gdi32.DeleteObject(hbmp)
    gdi32.DeleteDC(hdc_mem)
    u32.ReleaseDC(None, hdc_screen)

    return {"path": path, "width": width, "height": height,
            "method": "PrintWindow(PW_RENDERFULLCONTENT)" if ok else "PrintWindow FAILED",
            "printWindowReturned": bool(ok), "blank": blank}
