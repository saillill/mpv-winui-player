"""Find the Styles.xaml block the design contract means by ":880-1021", and its span.

The contract cites `Styles.xaml:880-1021` as one of the authorities for Windows 11
typography and spacing. Measured, that range now contains a scrollbar ControlTemplate
and a card Style, and exactly two uses of the word Padding -- no FontSize and no
MpvSpace/MpvFont key. So the citation is stale: it was written when the token block
sat there, and the file grew above it.

This locates the actual token block by content (the MpvFont*/MpvSpace* keys and the
typographic Style definitions) rather than by guessing a delta, and prints its bounds
so the citation can be corrected to something true.
"""
import io
import os

XAML = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "mpv-winui", "mpv-winui", "Styles", "Styles.xaml")

rows = io.open(XAML, encoding="utf-8-sig").read().split("\n")


def key_line(kind):
    out = []
    for i, line in enumerate(rows, 1):
        if ('x:Key="%s"' % kind) in line:
            out.append(i)
    return out


font = key_line("MpvFont")
space = key_line("MpvSpace")
radius = key_line("MpvRadius")
size = [i for i, l in enumerate(rows, 1) if "<x:Double" in l]

for name, hits in (("MpvFont*", font), ("MpvSpace*", space),
                   ("MpvRadius*", radius), ("<x:Double>", size)):
    if hits:
        print("%-12s %d keys, lines %d..%d" % (name, len(hits), hits[0], hits[-1]))
    else:
        print("%-12s none" % name)

first = min([h for h in (font, space, radius, size) if h][0]) if any((font, space, radius, size)) else None
last = max([h for h in (font, space, radius, size) if h][-1]) if any((font, space, radius, size)) else None
print()
print("token block spans roughly %s..%s" % (first, last))

# What the stale range actually contains now, for the report.
seg = rows[879:1021]
print("range 880-1021 currently holds %d lines; FontSize x%d, MpvSpace x%d"
      % (len(seg), sum("FontSize" in l for l in seg),
         sum('x:Key="MpvSpace' in l for l in seg)))
