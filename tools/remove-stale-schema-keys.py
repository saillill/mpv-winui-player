"""Drop the 14 Schema* keys that no longer have a property behind them.

Removing six options from the schema regenerated AppLang.Gen.cs without their
label/description properties, and the localization checker caught the other half of
that change:

    ERROR: zh-CN.json: 14 stale key(s) with no AppLang property and no reader:
    14x Schema* (e.g. SchemaDesc_Mpv_cuda_decode_device)

Fourteen because each option had two strings (SchemaLabel_ and SchemaDesc_), and
zh-CN is the only language file that carries the generated Schema* keys at all --
the other eight are 724 keys behind, which settings-design-contract §7 records as a
known exception. So this touches one file, and it deletes keys rather than
translating them.

Only the keys whose property is really gone are removed; the list is derived from
the six option names, not guessed, and the script asserts the count.
"""
import io
import json
import sys

LANG = (r"C:\Users\z2637\Documents\DeepSeek\mpv\mpv-winui\mpv-winui"
        + "\\" + r"Languages\zh-CN.json")

GONE_OPTIONS = [
    "openal_direct_channels",
    "openal_num_buffers",
    "openal_num_samples",
    "cuda_decode_device",
    "input_gamepad",
    "demuxer_sbr_probesize",
]
STALE = set()
for opt in GONE_OPTIONS:
    STALE.add("SchemaLabel_Mpv_" + opt)
    STALE.add("SchemaDesc_Mpv_" + opt)
print("keys to remove:", len(STALE))

raw = io.open(LANG, "rb").read()
bom = raw.startswith(b"\xef\xbb\xbf")
text = raw.decode("utf-8-sig")
data = json.loads(text)
print("keys before:", len(data))

present = STALE & set(data)
missing = STALE - set(data)
print("present:", len(present), "already absent:", len(missing))

if missing:
    print("  (absent, nothing to do):", sorted(missing)[:4], "...")

for key in present:
    del data[key]

print("keys after:", len(data))
if len(present) != 14:
    sys.exit("refusing: removed %d, expected 14" % len(present))

io.open(LANG, "wb").write(
    (json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    .encode("utf-8-sig" if bom else "utf-8"))
print("wrote", LANG)
