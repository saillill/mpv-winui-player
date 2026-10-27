"""Remove only the six options the bundled mpv does not have.

The first attempt at this regenerated the whole schema from the DLL's option-name
list and dropped 54 entries. That was too much, and the reason matters:

    vo, zimg, opengl, vulkan, angle, egl, openal, vaapi, vo-sixel

Those are PLATFORM options. They are absent from this Windows build because the
feature is compiled out, not because mpv removed them -- the same mpv on Linux, or
a build with OpenGL enabled, has all of them. Deleting them would mean the editor
stops offering options that become valid again the moment the user swaps in a
different mpv build, and this project ships the schema so the editor works without
one.

What is actually broken is narrower and was proven separately: six specific options
answer "property not found" live, AND their removal does not depend on how this
build was configured:

    openal-direct-channels, openal-num-buffers, openal-num-samples
        OpenAL support is not built into this mpv at all; these are not
        configuration-dependent.
    cuda-decode-device
        absent even though the build supports cuda (it has nvdec and cuda hwdec).
    input-gamepad
        absent; gamepad support is not compiled in.
    demuxer-sbr-probesize
        absent, while its siblings demuxer-lavf-probesize and
        demuxer-lavf-buffersize are present -- so this is a removed option, not a
        build switch.

The evidence for those six, in order: they were in the rejected startup commands
and answered "property not found" over IPC; a second check read them straight from
the bundled DLL (mpv v0.41.0-dev-ge8673660a) and all six were empty. Both agree.

This script removes exactly those six and asserts the count, so a future run cannot
silently widen into a platform sweep.
"""
import io
import json
import os
import sys

SCHEMA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                      "mpv-winui", "mpv-winui", "MpvConfOptions", "mpv-options.json")

GONE = {
    "openal-direct-channels",
    "openal-num-buffers",
    "openal-num-samples",
    "cuda-decode-device",
    "input-gamepad",
    "demuxer-sbr-probesize",
}


def main():
    raw = io.open(SCHEMA, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    entries = json.loads(raw.decode("utf-8-sig"))
    print("entries before:", len(entries))

    present = {e["name"] for e in entries}
    missing = GONE - present
    if missing:
        sys.exit("these were expected in the schema but are not: %s" % sorted(missing))

    kept = [e for e in entries if e["name"] not in GONE]
    removed = len(entries) - len(kept)
    print("removed:", removed)
    if removed != len(GONE):
        sys.exit("refusing: removed %d, expected %d" % (removed, len(GONE)))

    print("entries after:", len(kept))
    io.open(SCHEMA, "wb").write(
        (json.dumps(kept, ensure_ascii=False, indent=2) + "\n")
        .encode("utf-8-sig" if bom else "utf-8"))
    print("wrote", SCHEMA)


if __name__ == "__main__":
    sys.exit(main())
