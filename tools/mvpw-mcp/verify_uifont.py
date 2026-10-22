"""Does the UiFont setting reach the live UI, or is the write to
Application.Current.Resources too late for already-parsed XAML?

skin2 flagged the mechanism as suspect: WindowStyleManager.UpdateUiFont writes
`Application.Current.Resources["ContentControlThemeFontFamily"]`, and the
documented behaviour is that resources added at runtime do not re-apply to
elements whose style has already been resolved.

That predicts: with a distinctive font set, the visible text still renders in
the default font. This checks it by measuring, not by reading:

  * set UiFont to a font that is unmistakably different in metrics (Consolas
    is monospaced and much wider than Segoe UI at the same size)
  * launch, read the BoundingRectangle width of a fixed string (the window
    title text, `ShellTitleText`)
  * restore, launch, read it again

A width change means the font took effect; an identical width means it did not.
The title string is "MPV" (3 characters), which is short, so it also reports
the title of a second, longer label as a second sample.
"""
import json
import subprocess
import sys
import time

import mpvw_tools as T

REG = r"HKCU:\Software\Classes\Local Settings\Software\mpv-winui\mpv-winui\app"
SAMPLES = ["ShellTitleText", "ShellSettingsText", "TimeRemainingElement"]


def set_font(value):
    subprocess.run(["pwsh", "-NoProfile", "-Command",
                    f'Set-ItemProperty -Path "{REG}" -Name UiFont -Value "{value}" -Type String'],
                   capture_output=True, text=True)


def measure():
    snap = T.get_ui_snapshot(max_depth=14, redact_names=True)
    out = {}
    for e in snap["elements"]:
        if e["automationId"] in SAMPLES and e["rect"]:
            l, t, r, b = e["rect"]
            out[e["automationId"]] = {"w": r - l, "h": b - t}
    return out


results = {}
T.close_app()

for label, font in (("default", ""), ("consolas", "Consolas"), ("default2", "")):
    set_font(font)
    T.close_app()
    T.launch_app()
    time.sleep(2.0)
    results[label] = measure()
    print("%-9s font=%-10r %s" % (label, font, json.dumps(results[label])))

set_font("")
T.close_app()

a, b = results.get("default", {}), results.get("consolas", {})
changed = [k for k in SAMPLES if k in a and k in b and a[k] != b[k]]
print("\nchanged under Consolas: %s" % (changed or "NOTHING"))
verdict = ("the resource write does reach the live UI" if changed
           else "the setting has no visible effect (write is too late, as suspected)")
print("verdict -> " + verdict)

path = r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\uifont.json"
with open(path, "w", encoding="utf-8") as f:
    json.dump({"results": results, "changed": changed, "verdict": verdict},
              f, ensure_ascii=False, indent=1)
print("wrote " + path)
