"""The board's own proof: inject the real defect each assertion names.

A checker never seen red has unknown blind spots. This runs the board once per
injected defect and requires exactly the matching assertion to fail, then restores
the file and rebuilds.

A third injection - a zero-height media tile - used to live here, and it is gone
for a reason worth keeping: the card's Button is VerticalAlignment=Stretch, so the
height UIA reports is the grid CELL, not the tile. Collapsing the tile left that
number unchanged and the board stayed green. The assertion was removed rather than
kept as decoration; the tile's presence is covered by the cover pixel sample the
card research records.
"""
import io
import os
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
REPO = r"C:\Users\z2637\Documents\mpv-project\mpvw"
CARD_CS = os.path.join(REPO, "src", "mpvw-app", "Domains", "Playback",
                       "Controls", "LibraryCard.xaml.cs")

BOARD = [sys.executable, os.path.join(REPO, "tools", "ui-design-audit.py"), "--board", "cards"]
BUILD = ["dotnet", "build",
         os.path.join(REPO, "src", "mpvw-app", "mpv-winui.csproj"),
         "-c", "Release", "-p:Platform=x64",
         "-p:GenerateAppxPackageOnBuild=false", "-p:AppxPackageSigningEnabled=false",
         "-v:minimal"]


def read():
    with io.open(CARD_CS, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(text):
    with io.open(CARD_CS, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def build():
    r = subprocess.run(BUILD, capture_output=True, text=True, encoding="utf-8",
                       errors="replace", timeout=900)
    return r.returncode == 0


def run_board():
    r = subprocess.run(BOARD, capture_output=True, text=True, encoding="utf-8",
                       errors="replace", timeout=900)
    return r.returncode, (r.stdout + r.stderr)


original = read()
results = []


def attempt(label, mutate, expect):
    mutated = mutate(original)
    if mutated == original:
        results.append((label, False, "mutation did not apply"))
        return
    write(mutated)
    if not build():
        write(original)
        results.append((label, False, "injected build failed"))
        return
    code, out = run_board()
    write(original)
    hit = code != 0 and expect in out
    results.append((label, hit, "exit=%d, wanted %r in the output" % (code, expect)))
    print("  injected %-32s exit=%d  %s"
          % (label, code, "RED as expected" if hit else "NOT RED"))
    time.sleep(1)


attempt(
    "the card loses its name",
    lambda t: t.replace(
        "AutomationProperties.SetName(RootButton, TitleText.Text);",
        "AutomationProperties.SetName(RootButton, string.Empty);"),
    "empty name")

attempt(
    "the card is named from the wrong property",
    lambda t: t.replace(
        "AutomationProperties.SetName(RootButton, TitleText.Text);",
        "AutomationProperties.SetName(RootButton, Entry?.Path ?? string.Empty);"),
    "different property")

write(original)
build()

print()
ok = True
for label, passed, detail in results:
    print(("  PASS  " if passed else "  FAIL  ") + label + ("" if passed else "   (" + detail + ")"))
    ok = ok and passed
print("\nthe board can go red for every defect it names" if ok else "\nthe board has a blind spot")
sys.exit(0 if ok else 1)
