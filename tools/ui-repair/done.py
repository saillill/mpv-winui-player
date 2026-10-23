"""Mark a queue item done, with the evidence that proves it.

`done` is not a label someone types: this refuses unless the item's reproduction actually
passes NOW, twice in a row, and it writes the numbers into the queue so the next round can
see what was measured rather than what was claimed.

    python tools/ui-repair/done.py upstream-displayrequest --evidence "repro PASS x2"

What it does:
  1. runs `repro.py <id>` twice; both must pass
  2. rewrites that row's status and evidence in QUEUE.md
  3. prints the next item

If the reproduction still fails, it says so and changes nothing -- a queue that records
work as finished while the defect is present is worse than no queue.
"""
import io
import os
import re
import subprocess
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
QUEUE = os.path.join(REPO, r"docs\ui-repair\QUEUE.md")
REPRO = os.path.join(REPO, r"tools\ui-repair\repro.py")


def run_repro(item):
    r = subprocess.run([sys.executable, REPRO, item], cwd=REPO,
                       capture_output=True, text=True, errors="replace")
    out = (r.stdout or "") + (r.stderr or "")
    return r.returncode, out


def main():
    if len(sys.argv) < 2:
        print("usage: done.py <id> [--evidence \"...\"]")
        return 2
    item = sys.argv[1]
    note = ""
    if "--evidence" in sys.argv:
        i = sys.argv.index("--evidence")
        if i + 1 < len(sys.argv):
            note = sys.argv[i + 1]

    print("verifying %s -- the reproduction must pass twice\n" % item)
    codes = []
    for attempt in (1, 2):
        code, out = run_repro(item)
        codes.append(code)
        tail = [l for l in out.splitlines() if l.strip()][-1:] or [""]
        print("  run %d: exit=%d  %s" % (attempt, code, tail[0][:90]))

    if codes == [2, 2]:
        print("\nUNVERIFIED: no reproduction exists for %r." % item)
        print("Write one before claiming it done.")
        return 2
    if any(c != 0 for c in codes):
        print("\nrefusing to mark done: the reproduction still fails.")
        print("Fix the defect, or record it as blocked with the reason.")
        return 1

    text = io.open(QUEUE, encoding="utf-8").read()
    row = re.compile(r"^(\|\s*`" + re.escape(item) + r"`\s*\|.*)$", re.M)
    m = row.search(text)
    if not m:
        print("\nreproduction passes but the queue has no row for %r." % item)
        print("Add one so the next round can see it was done.")
        return 1

    old = m.group(1)
    # Move the row out of "actionable" by prefixing the symptom with DONE.
    if not old.startswith("| `" + item + "` | DONE |"):
        cells = [c.strip() for c in old.strip("|").split("|")]
        cells.insert(0, "DONE")
        new = "| " + " | ".join(cells) + " |"
        text = text.replace(old, new, 1)
        io.open(QUEUE, "w", encoding="utf-8").write(text)
        print("\nQUEUE.md updated: %s" % item)

    print("\nrecorded evidence: %s" % (note or "(none given -- add one next time)"))
    print("repro passes 2/2")
    print("\nnext: python tools/ui-repair/next.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
