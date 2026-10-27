"""Regenerate mpv-options.json from the DLL the app actually ships.

The schema has drifted: six of its 820 entries no longer exist in the bundled mpv
(openal-direct-channels, openal-num-buffers, openal-num-samples,
cuda-decode-device, input-gamepad, demuxer-sbr-probesize -- every one answers
"property not found"), so the settings window renders six rows that cannot do
anything and the startup batch tried to apply all six on every launch.

tools/gen-mpv-options.py needs an mpv.exe, which this machine does not have, and
downloading one would rebuild the schema from a DIFFERENT version -- which is how
the drift happened. So the table is read from the DLL that ships with the app
(mpv v0.41.0-dev-ge8673660a) and written in the shape the generator consumes.

Reading it: `options` is not a string property, so mpv_get_property_string returns
empty. This defines the mpv_node structs and walks the MPV_FORMAT_NODE result.

What is preserved from the existing file: each surviving entry's group, desc,
link and values, because the DLL knows the option's NAME and DEFAULT but not the
prose or the grouping this project chose. Only the name set and the defaults come
from the DLL, and only options that already had a row are kept -- this is a
correction of drift, not a re-scoping of which options the app exposes.
"""
import ctypes
import io
import json
import os
import sys

DLL = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "mpv-winui", "libs", "bin", "x64", "mpv-2.dll")
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "mpv-winui", "mpv-winui", "MpvConfOptions", "mpv-options.json")

MPV_FORMAT_FLAG = 3
MPV_FORMAT_INT64 = 4
MPV_FORMAT_DOUBLE = 5
MPV_FORMAT_NODE = 6
MPV_FORMAT_NODE_ARRAY = 7
MPV_FORMAT_NODE_MAP = 8


class MpvNodeUnion(ctypes.Union):
    _fields_ = [
        ("flag", ctypes.c_int),
        ("int64", ctypes.c_int64),
        ("double_", ctypes.c_double),
        ("string", ctypes.c_char_p),
        ("list", ctypes.c_void_p),
        ("ba", ctypes.c_void_p),
    ]


class MpvNode(ctypes.Structure):
    pass


MpvNode._fields_ = [("u", MpvNodeUnion), ("format", ctypes.c_int)]


class MpvNodeList(ctypes.Structure):
    pass


MpvNodeList._fields_ = [
    ("num", ctypes.c_int),
    ("values", ctypes.POINTER(MpvNode)),
    ("keys", ctypes.POINTER(ctypes.c_char_p)),
]


def node_to_python(node):
    fmt = node.format
    if fmt == MPV_FORMAT_FLAG:
        return bool(node.u.flag)
    if fmt == MPV_FORMAT_INT64:
        return int(node.u.int64)
    if fmt == MPV_FORMAT_DOUBLE:
        return float(node.u.double_)
    if fmt == 1:  # MPV_FORMAT_STRING
        return (node.u.string or b"").decode("utf-8", "replace")
    if fmt in (MPV_FORMAT_NODE_MAP, MPV_FORMAT_NODE_ARRAY):
        lst = ctypes.cast(node.u.list, ctypes.POINTER(MpvNodeList)).contents
        if fmt == MPV_FORMAT_NODE_ARRAY:
            return [node_to_python(lst.values[i]) for i in range(lst.num)]
        out = {}
        for i in range(lst.num):
            key = (lst.keys[i] or b"").decode("utf-8", "replace")
            out[key] = node_to_python(lst.values[i])
        return out
    return None


def main():
    mpv = ctypes.CDLL(DLL)
    mpv.mpv_create.restype = ctypes.c_void_p
    mpv.mpv_initialize.argtypes = [ctypes.c_void_p]
    mpv.mpv_initialize.restype = ctypes.c_int
    mpv.mpv_get_property.argtypes = [ctypes.c_void_p, ctypes.c_char_p,
                                     ctypes.c_int, ctypes.POINTER(MpvNode)]
    mpv.mpv_get_property.restype = ctypes.c_int
    mpv.mpv_free_node_contents.argtypes = [ctypes.POINTER(MpvNode)]
    mpv.mpv_terminate_destroy.argtypes = [ctypes.c_void_p]

    handle = mpv.mpv_create()
    if mpv.mpv_initialize(handle) < 0:
        sys.exit("mpv_initialize failed")

    node = MpvNode()
    rc = mpv.mpv_get_property(handle, b"options", MPV_FORMAT_NODE,
                              ctypes.byref(node))
    if rc < 0:
        sys.exit("mpv_get_property(options) failed: %d" % rc)

    table = node_to_python(node)
    mpv.mpv_free_node_contents(ctypes.byref(node))
    # `options` is a LIST OF NAMES, not a map with defaults: measured, 826 strings
    # ("v", "playlist", "{", ...). An earlier version of this script assumed a map
    # and died on it. So only the set of valid names comes from the DLL here; the
    # defaults stay as the schema already had them.
    if not isinstance(table, list):
        sys.exit("options node is %r, not a list" % type(table))
    live_names = {n for n in table if isinstance(n, str)}
    print("option names in the DLL:", len(live_names))

    old = json.load(io.open(OUT, encoding="utf-8-sig"))
    print("entries in the shipped schema:", len(old))

    kept, dropped = [], []
    for entry in old:
        if entry["name"] in live_names:
            kept.append(entry)
        else:
            dropped.append(entry["name"])

    added = sorted(live_names - {e["name"] for e in old})
    print("\ndropped (absent from the DLL): %d" % len(dropped))
    for n in dropped:
        print("   -", n)
    print("available in the DLL but not in the schema: %d (left alone -- this is a"
          " drift fix, not a re-scope)" % len(added))

    io.open(OUT, "w", encoding="utf-8", newline="\n").write(
        json.dumps(kept, ensure_ascii=False, indent=2) + "\n")
    print("\nwrote %d entries to %s" % (len(kept), OUT))

    mpv.mpv_terminate_destroy(handle)


if __name__ == "__main__":
    sys.exit(main())
