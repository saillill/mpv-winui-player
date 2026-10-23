"""Print the item to work on next, with everything needed to start.

The queue lives in docs/ui-repair/QUEUE.md as markdown tables. This parses the row for a
given id (or the first actionable row) and prints: the symptom, the evidence, the domain
README to read first, and how to reproduce.

Keeping the queue in a hand-editable markdown file, rather than a database, is deliberate:
a new conversation has to be able to read and edit it with no setup, and the review step
in PLAYBOOK.md is itself a human judgement.
"""
import io
import os
import re
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
QUEUE = os.path.join(REPO, r"docs\ui-repair\QUEUE.md")
DOMAINS = os.path.join(REPO, r"mpv-winui\mpv-winui\Modules")

# | `id` | domain | kind | repro | symptom | evidence |
ROW = re.compile(r"^\|\s*`([^`]+)`\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*"
                 r"([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*$")

DOMAIN_DIR = {
    "activation": "Activation", "appmodel": "AppModel", "common": "Common",
    "filesystem": "FileSystem", "language": "Language", "mediainfo": "MediaInfo",
    "menu": "Menu", "motion": "Motion", "mpv": "Mpv", "mpvconf": "MpvConf",
    "player": "Player", "settings": "Settings", "playback": "Player",
    "library": "Player", "history": "Player", "shell": "Shell",
}


def parse():
    rows = []
    if not os.path.exists(QUEUE):
        sys.exit("queue not found: " + QUEUE)
    section = ""
    for line in io.open(QUEUE, encoding="utf-8").read().splitlines():
        if line.startswith("## "):
            section = line[3:].strip()
        m = ROW.match(line)
        if m:
            rows.append({
                "id": m.group(1).strip(), "domain": m.group(2).strip(),
                "kind": m.group(3).strip(), "repro": m.group(4).strip(),
                "symptom": m.group(5).strip(), "evidence": m.group(6).strip(),
                "section": section,
            })
    return rows


def readme_for(domain):
    d = DOMAIN_DIR.get(domain.lower())
    if not d:
        return None
    p = os.path.join(DOMAINS, d, "README.md")
    return p if os.path.exists(p) else None


def show(row, n=None):
    tag = "[%s] " % n if n else ""
    print("%s%s  (%s / %s)" % (tag, row["id"], row["domain"], row["kind"]))
    print("  symptom : %s" % row["symptom"])
    print("  repro   : %s" % row["repro"])
    print("  evidence: %s" % row["evidence"])
    r = readme_for(row["domain"])
    if r:
        print("  read first: %s" % r)
    else:
        print("  read first: mpv-winui/mpv-winui/DOMAINS.md (no README for this domain)")
    if row["repro"].startswith("unverified"):
        print("  NOTE: unverified -- prove it exists before changing any product code")
        print("        (PLAYBOOK.md step 1; two independent measurements)")
    print("  next command: python tools/ui-repair/repro.py %s" % row["id"])
    print()


def main():
    rows = parse()
    actionable = [r for r in rows if "可立即开工" in r["section"]]

    if len(sys.argv) > 1:
        want = sys.argv[1]
        hit = [r for r in rows if r["id"] == want]
        if not hit:
            print("no such id: %s" % want)
            print("\nknown ids:")
            for r in rows:
                print("   %s" % r["id"])
            return 1
        show(hit[0])
        return 0

    print("actionable items: %d" % len(actionable))
    print()
    for i, r in enumerate(actionable, 1):
        show(r, i)

    if not actionable:
        print("nothing actionable in section A.")
        print("Section C lists domains that need a measurement first --")
        print("run: python tools/ui-repair/scan.py")
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
