"""Verify the generated enum set against the options that still get quoted."""
import io
import json
import os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GEN = os.path.join(REPO, r"mpv-winui\mpv-winui\Domains\MpvConf\MpvSettings.Gen.cs")
CONF = os.path.join(os.environ["LOCALAPPDATA"], "mpv-winui", "mpv", "mpv.conf")

src = io.open(GEN, encoding="utf-8-sig").read()

start = src.find("GeneratedEnumOptions")
end = src.find("};", start)
enum_block = src[start:end]

names_start = src.find("GeneratedOptionNames")
names_end = src.find("};", names_start)
names_block = src[names_start:names_end]

quoted = []
if os.path.exists(CONF):
    for line in io.open(CONF, encoding="utf-8", errors="replace").read().splitlines():
        if "=" in line and not line.lstrip().startswith("#"):
            k, _, v = line.partition("=")
            if k and v.startswith('"') and v.endswith('"'):
                quoted.append(k.strip())

print("options still quoted in mpv.conf: %d" % len(quoted))
print()
print("%-26s %-10s %-10s %s" % ("option", "in enums", "generated", "verdict"))
for name in quoted:
    in_enum = ('"%s"' % name) in enum_block
    generated = ('= "%s"' % name) in names_block or ('"%s",' % name) in names_block
    if in_enum:
        verdict = "BUG: enum still quoted"
    elif generated:
        verdict = "ok: free text, quotes may be needed"
    else:
        verdict = "hand-written row"
    print("%-26s %-10s %-10s %s" % (name, in_enum, generated, verdict))
