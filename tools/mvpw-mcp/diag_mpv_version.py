"""Get the exact mpv build behind the bundled mpv-2.dll, then the option list.

The schema (MpvConfOptions/mpv-options.json) was produced by `mpv --list-options`
against some mpv build, and the drift this is meant to fix is that six options in
it no longer exist in the mpv the app actually runs. Regenerating against a
different version would just move the drift, so the first job is to read the
version out of the DLL that ships with the app.

mpv exports mpv_client_api_version and reports its version string through the
client API; loading it here and asking is the only way to know without an mpv.exe.
"""
import ctypes
import json
import sys
import os

# Repo root, derived from this file (it lives at <repo>/tools/...), so renaming or
# moving the checkout does not silently repoint these paths at an old copy.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

DLL = REPO + r"\src\libs\bin\x64\mpv-2.dll"


def main():
    mpv = ctypes.CDLL(DLL)

    # mpv_client_api_version returns (major << 16) | minor.
    mpv.mpv_client_api_version.restype = ctypes.c_ulong
    api = mpv.mpv_client_api_version()
    print("client API: %d.%d" % (api >> 16, api & 0xFFFF))

    # Create a handle with no window, then ask for mpv-version.
    mpv.mpv_create.restype = ctypes.c_void_p
    handle = mpv.mpv_create()
    if not handle:
        sys.exit("mpv_create returned NULL")
    print("handle:", hex(handle))

    mpv.mpv_initialize.argtypes = [ctypes.c_void_p]
    mpv.mpv_initialize.restype = ctypes.c_int
    rc = mpv.mpv_initialize(handle)
    print("mpv_initialize:", rc)

    mpv.mpv_get_property_string.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
    mpv.mpv_get_property_string.restype = ctypes.c_void_p
    val = mpv.mpv_get_property_string(handle, b"mpv-version")
    if val:
        print("mpv-version:", ctypes.cast(val, ctypes.c_char_p).value.decode())
        mpv.mpv_free.argtypes = [ctypes.c_void_p]
        mpv.mpv_free(val)
    else:
        print("mpv-version: <null>")

    # The whole option table, as one string... it is huge, so probe instead.
    for name in ("openal-direct-channels", "openal-num-buffers",
                 "openal-num-samples", "cuda-decode-device", "input-gamepad",
                 "demuxer-sbr-probesize"):
        v = mpv.mpv_get_property_string(handle, name.encode())
        print("  %-26s %s" % (name, "<absent>" if not v else ctypes.cast(
            v, ctypes.c_char_p).value.decode()[:40]))
        if v:
            mpv.mpv_free(v)

    mpv.mpv_terminate_destroy.argtypes = [ctypes.c_void_p]
    mpv.mpv_terminate_destroy(handle)
    print("terminated")


if __name__ == "__main__":
    sys.exit(main())
