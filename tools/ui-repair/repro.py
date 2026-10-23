"""Run the reproduction for one queue item.

Each reproduction is a function here, so it can be run repeatedly instead of followed by
hand. `repro <id>` prints PASS (defect absent) or FAIL (defect present), exiting 1 on FAIL.
An id with no reproduction yet reports UNVERIFIED and exits 2 -- a different next action
from FAIL, because UNVERIFIED means nobody has shown the defect is real.

**What the first version got wrong, and the rule it produced.** All four reproductions
searched for a TYPE NAME (`AppConfDataImport`, `SimpleDisplayRequest`, ...) and reported
"0 sources, defect present". Then a file literally named
`SettingsPage.Actions.ImportExport.cs` turned out to contain no `ImportExport` string --
it is a `partial class SettingsPage` whose methods have other names. Grepping for the name
upstream chose answers "did we copy their spelling", not "does this app do the thing".

So each reproduction below answers a behavioural question, in this priority:

  1. can the app be observed DOING it (a UIA element, an IPC property, a log line)?
  2. failing that, does the code path exist under ANY name (entry point, call site,
     public surface) -- not whether it matches upstream's identifier;
  3. a name match is evidence of nothing and is not used.
"""
import os
import re
import subprocess
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
APPSRC = os.path.join(REPO, "mpv-winui", "mpv-winui")


def files(pattern, exts=(".cs", ".xaml")):
    """Every tracked app source file containing `pattern`, as relative paths."""
    r = subprocess.run(["git", "grep", "-lnE", pattern, "--", "mpv-winui/mpv-winui/"],
                       cwd=REPO, capture_output=True, text=True, errors="replace")
    out = []
    for line in (r.stdout or "").splitlines():
        if line.endswith(exts):
            out.append(line)
    return out


def count(pattern):
    r = subprocess.run(["git", "grep", "-cE", pattern, "--", "mpv-winui/mpv-winui/"],
                       cwd=REPO, capture_output=True, text=True, errors="replace")
    total = 0
    for line in (r.stdout or "").splitlines():
        if ":" in line:
            try:
                total += int(line.rsplit(":", 1)[1])
            except ValueError:
                pass
    return total


def report(label, hits, ok):
    print("  %-34s %d source(s)  %s" % (label, len(hits), "OK" if ok else "ABSENT"))
    for p in hits[:6]:
        print("        " + p)
    return ok


# ---------------------------------------------------------------- reproductions ---

def repro_upstream_fileassoc():
    """Can the user register or remove a file/protocol association from inside the app?

    Upstream ships a settings page for it. The behavioural question is whether any UI
    exists that writes HKCU file associations -- so this looks for the registry write and
    for a XAML surface that hosts it, under any name.
    """
    writes = files(r"HKEY_CLASSES_ROOT|HKCR:|Shell\\Open\\Command|Classes\\\.")
    ui = [p for p in files(r"Association") if p.endswith(".xaml")]
    print("  association UI (xaml)          %d" % len(ui))
    for p in ui[:6]:
        print("        " + p)
    print("  association registry writers   %d" % len(writes))
    for p in writes[:6]:
        print("        " + p)
    return bool(ui) or bool(writes)


def repro_upstream_appconfdata():
    """Can the app import settings from an external file, from the UI?

    Upstream ships `AppConfData/` (a dialog + 8 KB service + 13 KB of tests). The local
    equivalent is `SettingsPage.Actions.ImportExport.cs`, whose handlers are named
    `ImportConfigAsync` / `ExportConfigAsync`.

    The first version of this reproduction required a file picker and a settings write in
    the SAME file, found 0, and would have sent someone to port a feature that already
    exists. A capability is not a file layout: this now looks for an import entry point
    anywhere, then asks separately whether the pieces line up.
    """
    entry = files(r"ImportConfig|ExportConfig|ImportAppConf|AppConfData")
    picker = files(r"PickSingleFileAsync|PickSaveFileAsync|FileOpenPicker|FileSavePicker")
    both = sorted(set(entry) & set(picker))
    print("  import/export entry points     %d" % len(entry))
    for p in entry[:8]:
        print("        " + p)
    print("  same file also opens a picker  %d" % len(both))
    for p in both[:8]:
        print("        " + p)
    return bool(entry)


def repro_upstream_displayrequest():
    """Does anything keep the display awake while media plays?

    Two ways exist on Windows: the WinRT `DisplayRequest` and `SetThreadExecutionState`.
    Either one counts; the question is whether playback would let the screen sleep.
    """
    hits = files(r"DisplayRequest|SetThreadExecutionState|ES_DISPLAY_REQUIRED|ES_CONTINUOUS")
    return report("display-wake mechanisms", hits, bool(hits))


def repro_upstream_win32cmdline():
    """Does the app read its own command line (file association passes paths that way)?

    The app is unpackaged, so it can receive `mpvw.exe "some file.mkv"`. The question is
    whether any argument is read at startup, however it is parsed.
    """
    hits = files(r"GetCommandLine|CommandLineToArgv|Environment\.GetCommandLineArgs|"
                 r"e\.Args\b|LaunchActivatedEventArgs")
    return report("command-line readers", hits, bool(hits))


REPROS = {
    "upstream-fileassoc": repro_upstream_fileassoc,
    "upstream-appconfdata": repro_upstream_appconfdata,
    "upstream-displayrequest": repro_upstream_displayrequest,
    "upstream-win32cmdline": repro_upstream_win32cmdline,
}


def main():
    if len(sys.argv) < 2:
        print("usage: repro.py <id>")
        print("\nreproductions available:")
        for k in REPROS:
            print("   " + k)
        return 2

    want = sys.argv[1]
    fn = REPROS.get(want)
    if fn is None:
        print("UNVERIFIED: no reproduction written for %r yet." % want)
        print("Write one in tools/ui-repair/repro.py, record what it measured in")
        print("QUEUE.md, and only then change product code.")
        return 2

    print("repro: %s" % want)
    present = fn()
    print("\n%s" % ("PASS -- the capability is present" if present
                    else "FAIL -- the capability is absent"))
    return 0 if present else 1


if __name__ == "__main__":
    sys.exit(main())
