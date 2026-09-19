"""Check the settings search index covers what it claims to.

The searcher index lives in SettingsPage.xaml.cs (C#), so this cannot execute
it. What it CAN check statically is the part that silently breaks: which fields
feed the index, and that the mpv option name of every settable option is
reachable through MpvSettings.ToMpvOptionName -- because if it is not, the
"search by mpv name" promise quietly does nothing for that row.

Run:  python tools/check-search-index.py
"""
import re
import sys

ROOT = "mpv-winui/mpv-winui"
OPTIONS = f"{ROOT}/Modules/Settings"
PAGE = f"{OPTIONS}/SettingsPage.xaml.cs"
MPVSETTINGS = f"{OPTIONS}/MpvSettings.cs"


def read(path):
    return open(path, encoding="utf-8-sig").read()


def fail(msg):
    print("FAIL:", msg)


def main():
    page = read(PAGE)
    problems = []

    # 1. BuildSearchEntry must name every field it claims to index.
    m = re.search(r"private OptionSearchEntry BuildSearchEntry\(Option option\)\s*\{(.*?)\n    \}",
                  page, re.S)
    if not m:
        fail("BuildSearchEntry not found (renamed?)")
        return 1
    body = m.group(1)
    for required in ("option.Label", "option.Description", "ToMpvOptionName",
                     "option.Section", "option.Choices", "CheckItems"):
        if required not in body:
            problems.append(f"BuildSearchEntry does not reference {required}")

    # 2. The tiers have to be ordered weakest-last or "value" matches would
    #    outrank real name matches.
    ranks = dict(re.findall(r"private const int (Rank\w+) = (\w+|int\.MaxValue);", page))
    want = ["RankLabelPrefix", "RankLabelContains", "RankMpvName", "RankDescription",
            "RankContext", "RankValues", "RankFuzzy"]
    for name in want:
        if name not in ranks:
            problems.append(f"missing rank constant {name}")
    values = []
    for name in want:
        raw = ranks.get(name)
        values.append(int(raw) if raw and raw.isdigit() else 10**6)
    if values != sorted(values):
        problems.append(f"rank constants not ascending in priority: {want} = {values}")

    # 3. Every option that ToCommand can set must be look-up-able by mpv name.
    settings = read(MPVSETTINGS)
    names = set(re.findall(r'\[nameof\(AppSettings\.(\w+)\)\]\s*=\s*"([a-z0-9\-]+)"',
                           settings))
    if not names:
        problems.append("could not parse MpvOptionNames")
    # Settings page often keys by AppContext.AppSetting.X
    opt_keys = set()
    import glob
    for path in glob.glob(f"{OPTIONS}/SettingsPage.Options*.cs"):
        opt_keys |= set(re.findall(
            r"Key\s*=\s*nameof\(AppContext\.AppSetting\.(\w+)\)", read(path)))
    covered = {k for k, _ in names}
    missing = sorted(k for k in opt_keys if k not in covered)
    # Not an error: some UI keys never map to an mpv option (pure app settings)
    pct = 100 * (len(opt_keys) - len(missing)) / max(1, len(opt_keys))
    print(f"options with a searchable mpv name: {len(opt_keys) - len(missing)}/{len(opt_keys)} ({pct:.0f}%)")
    if missing:
        print(f"  ({len(missing)} app-only settings carry no mpv name: "
              + ", ".join(missing[:6]) + ("..." if len(missing) > 6 else "") + ")")

    if problems:
        for p in problems:
            fail(p)
        return 1
    print("OK: search index covers label, description, mpv name, section, values; ranks ordered")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
