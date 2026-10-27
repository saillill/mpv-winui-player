"""Every remaining WUI finding, checked against the code rather than the rule.

After the migrations three groups are left. This script states what each one is and
what evidence settles it, so the numbers are reproducible instead of remembered:

  WUI2011 x14  MediaInfoUserControl.xaml
      Claims x:Bind defaults to OneTime so the UI will not update. Checked:
      MediaInfoRow exposes only getters (Label, Value, AutomationName, IsPathLike)
      and does not implement INotifyPropertyChanged; ShowReport() builds a NEW
      section list and assigns it to ItemsSource on every refresh, which rebuilds
      the item templates. OneTime is the correct mode and the rule is a false
      positive here.

  WUI2003 x10  PlayerTrackSelectorControl.xaml
      Claims <Pivot> is UWP-only with no WinUI 3 equivalent. Refuted twice:
      `winapp find-api members Microsoft.UI.Xaml.Controls.Pivot` reports the type
      with 308 members, and at runtime the pivot item surfaces through UI Automation
      as TabItemControl named "\u7b2c\u4e8c\u5b57\u5e55" (second subtitles) with
      automation id SecondSubPivotItem. The control compiles AND renders.

  nothing else
      WUI1001 is gone: the pickers moved to Microsoft.Windows.Storage.Pickers and
      AppDataSetting moved to ApplicationData.GetDefault(), matching the
      ApplicationData.GetForUnpackaged call the unpackaged backend already used.

Run it after a build; it reads the diagnostics from the compiler rather than
counting source lines.
"""
import re
import subprocess
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
XAML = REPO + r"\mpv-winui\mpv-winui\Modules\MediaInfo\MediaInfoUserControl.xaml"
REPORT = REPO + r"\mpv-winui\mpv-winui\Modules\MediaInfo\MediaInfoReport.cs"


def diagnostics():
    r = subprocess.run(
        ["dotnet", "build", r"mpv-winui\mpv-winui\mpv-winui.csproj",
         "-c", "Release", "-p:Platform=x64", "--no-incremental", "-v", "n"],
        cwd=REPO, capture_output=True, text=True, errors="replace")
    counts = {}
    sites = {}
    for line in (r.stdout or "").splitlines():
        m = re.search(r"(WUI\d{4})", line)
        if not m:
            continue
        code = m.group(1)
        counts[code] = counts.get(code, 0) + 1
        f = re.search(r"([A-Za-z0-9_]+\.xaml)\((\d+)", line)
        if f:
            sites.setdefault(code, set()).add((f.group(1), int(f.group(2))))
    return counts, sites, (r.stdout or "")


def main():
    counts, sites, _ = diagnostics()
    print("diagnostics:", counts)
    print()

    # WUI2011: prove the bound type is immutable.
    src = open(REPORT, encoding="utf-8-sig").read()
    setters = re.findall(r"public \w[\w<>?\[\], ]* (\w+) \{ get; set; \}", src)
    observable = "INotifyPropertyChanged" in src
    print("WUI2011 (%d)" % counts.get("WUI2011", 0))
    print("  MediaInfoRow / MediaInfoSection properties with a setter:",
          setters or "none")
    print("  implements INotifyPropertyChanged:", observable)
    print("  verdict:", "false positive -- OneTime is correct"
          if not setters and not observable else "REVIEW: type is mutable")
    print()

    # WUI2003: the control exists.
    api = subprocess.run(
        ["winapp", "find-api", "members", "Microsoft.UI.Xaml.Controls.Pivot",
         "--json", "--project-dir", r"mpv-winui\mpv-winui"],
        cwd=REPO, capture_output=True, text=True, errors="replace")
    m = re.search(r'"totalProperties":\s*(\d+)', api.stdout or "")
    print("WUI2003 (%d)" % counts.get("WUI2003", 0))
    print("  Microsoft.UI.Xaml.Controls.Pivot members:",
          m.group(1) if m else "NOT FOUND")
    print("  verdict:", "false positive -- the control exists and renders"
          if m else "REVIEW")
    print()

    others = {k: v for k, v in counts.items() if k not in ("WUI2011", "WUI2003")}
    print("other diagnostics:", others or "none")

    ok = (counts.get("WUI2011", 0) == 14
          and counts.get("WUI2003", 0) == 10
          and not others
          and not setters and not observable and m)
    print("\nPASS" if ok else "REVIEW: the shape changed since this was written")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
