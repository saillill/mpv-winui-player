"""Report the true counts the documents keep getting wrong, so a fix can be
verified mechanically instead of by re-reading tables.

Nothing here is hard-coded: every number is derived from the tree.
"""
import glob
import io
import json
import os
import re

ROOT = r"C:\Users\z2637\Documents\DeepSeek\mpv"
APP = os.path.join(ROOT, "mpv-winui", "mpv-winui")
SET = os.path.join(APP, "Modules", "Settings")
LANG = os.path.join(APP, "Modules", "Language")


def rows(path):
    return len(re.findall(r"\bnew Option\b", io.open(path, encoding="utf-8-sig").read()))


def props(path):
    return len(re.findall(r"public string \w+ \{ get; set; \}", io.open(path, encoding="utf-8-sig").read()))


gen = os.path.join(SET, "SettingsPage.Options.Gen.cs")
hand = [p for p in sorted(glob.glob(os.path.join(SET, "SettingsPage.Options.*.cs")))
        if not p.endswith(".Gen.cs")]

generated = rows(gen)
handwritten = sum(rows(p) for p in hand)

lang_files = sorted(glob.glob(os.path.join(APP, "Languages", "*.json")))
schema_per_file = {}
for f in lang_files:
    t = io.open(f, encoding="utf-8-sig").read()
    schema_per_file[os.path.basename(f)] = len(re.findall(r'"(?:SchemaLabel|SchemaDesc)_', t))

applang = {os.path.basename(p): props(p) for p in sorted(glob.glob(os.path.join(LANG, "AppLang*.cs")))}

checks = {
    "generated_rows": generated,
    "handwritten_rows": handwritten,
    "total_rows": generated + handwritten,
    "schema_label_props": props(os.path.join(LANG, "AppLang.Gen.cs")),
    "schema_keys_in_zh_CN": schema_per_file.get("zh-CN.json", 0),
    "schema_keys_in_en_US": schema_per_file.get("en-US.json", 0),
    "languages": len(lang_files),
    "applang_props_total": sum(applang.values()),
    "applang_by_file": applang,
    "checker_scripts": len(glob.glob(os.path.join(ROOT, "tools", "check-*.py"))),
    "tools_files": len([p for p in glob.glob(os.path.join(ROOT, "tools", "*"))
                        if os.path.isfile(p)]),
}

schema = json.load(io.open(os.path.join(APP, "MpvConfOptions", "mpv-options.json"),
                           encoding="utf-8-sig"))
checks["schema_entries"] = len(schema)

print(json.dumps(checks, indent=1, ensure_ascii=False))
