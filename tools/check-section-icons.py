#!/usr/bin/env python3
"""Report which Segoe Fluent Icons codepoints the settings sections use.

Used when adding a section: the settings shell renders one icon per section
card, and `tools/check-sections.py` requires them to be distinct. Picking a
glyph by eye and finding out it collides later is avoidable, so this prints
the used set and the free slots around it.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC = REPO / "mpv-winui" / "mpv-winui" / "Domains" / "Settings" / "Layout" / "SettingsSections.cs"

# The escape appears in the source as \uE7C4 -- four hex digits after a
# literal backslash-u. Writing that as a raw string keeps Python from
# trying to build a unicode escape out of the pattern itself.
PATTERN = re.compile(r"\\u([0-9A-Fa-f]{4})")


def main() -> int:
    src = SRC.read_text(encoding="utf-8-sig")
    head, marker, tail = src.partition("---- schema-generated sections ----")

    # Everything after the marker is not all generated sections: the
    # FallbackIcon constant lives there too, and it is deliberately the same
    # glyph as one of the sections (that is what "fallback" means). Only the
    # rows count, so the entries are picked by their shape.
    gen_rows = re.findall(r'new\("(Section\w+)",\s*"\\u([0-9A-Fa-f]{4})"', tail)
    gen_ids = [sid for sid, _ in gen_rows]
    gen_codes = [int(code, 16) for _, code in gen_rows]

    used = {int(m, 16) for m in PATTERN.findall(head)}

    print(f"hand-written sections : {len(used)} distinct icons")
    print(f"generated sections    : {len(gen_rows)}")
    for sid in gen_ids:
        print(f"    {sid}")

    # The codepoints in `used` / `gen_codes` are ints parsed from "\uE701", i.e. 0xE701.
    # Formatting them as f"E{c:03X}" therefore produces "EE701" -- the letter E, plus the
    # E that is already part of the number. Measured: 0xE701 -> 'EE701'. This is the
    # checker whose whole job is telling you which glyph to avoid, so printing a glyph
    # name that appears nowhere makes the finding unusable. `:04X` is the correct width:
    # the Segoe Fluent range used here is E700-EA00, four hex digits including the E.
    def glyph(c):
        return f"\\u{c:04X}"

    collisions = sorted(set(gen_codes) & used)
    print(f"generated vs hand-written collisions : "
          f"{', '.join(glyph(c) for c in collisions) if collisions else 'none'}")

    dupes = {c for c in gen_codes if gen_codes.count(c) > 1}
    print(f"collisions among generated           : "
          f"{', '.join(glyph(c) for c in sorted(dupes)) if dupes else 'none'}")

    if len(sys.argv) > 1 and sys.argv[1] == "--free":
        need = int(sys.argv[2]) if len(sys.argv) > 2 else 9
        taken = used | set(gen_codes)
        free = [c for c in range(0xE700, 0xEA00) if c not in taken]
        print(f"\n{len(free)} free slots in E700-E9FF; first {need}:")
        for c in free[:need]:
            print(f"  \\u{c:04X}")

    return 1 if (collisions or dupes) else 0


if __name__ == "__main__":
    sys.exit(main())
