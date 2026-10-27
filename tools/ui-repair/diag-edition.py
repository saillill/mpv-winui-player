"""Why does `edition` have generated language keys but no generated row?"""
import io
import json
import re
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, REPO + r"\tools")

src = io.open(REPO + r"\tools\gen-schema-options.py", encoding="utf-8-sig").read()
ns = {"__file__": REPO + r"\tools\gen-schema-options.py", "__name__": "g"}
exec(compile(src[:src.index("def main")], "g", "exec"), ns)

schema = json.load(io.open(
    REPO + r"\mpv-winui\mpv-winui\MpvConfOptions\mpv-options.json", encoding="utf-8-sig"))
entry = [x for x in schema if x["name"] == "edition"][0]

print("schema entry :", json.dumps(entry, ensure_ascii=False))
print("deprecated   :", entry.get("deprecated"))
print("is_denied    :", ns["is_denied"]("edition"))

mpv = io.open(REPO + r"\mpv-winui\mpv-winui\Domains\MpvConf\MpvSettings.cs",
              encoding="utf-8-sig").read()

wired = set(re.findall(
    r'\[\s*(?:nameof\(AppSettings\.)?\w+\s*\]\s*=\s*"([a-z0-9\-]+)"', mpv))
wired |= set(re.findall(r"\bset ([a-z][a-z0-9\-]*)\s", mpv))
print("in hand-written map:", "edition" in wired)

rows, _ = ns["load_generated"]()
names = {r["name"] for r in rows}
print("in generated rows  :", "edition" in names)

# Which schema entries have enum values with exactly one choice? Those are the ones a user
# cannot change, so the generator may be treating them as pointless.
single = []
for e in schema:
    for v in e.get("values") or []:
        en = v.get("enum")
        if en and len(en) == 1:
            single.append((e["name"], en[0].get("value")))
print("\nsingle-choice enum options in the schema:", len(single))
for n, v in single[:20]:
    print("   %-30s only %r   generated=%s" % (n, v, n in names))
