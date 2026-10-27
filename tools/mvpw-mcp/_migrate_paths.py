"""Replace the hardcoded deploy path in the verifiers with paths.deploy_dir().

Run once, after the 2026-10-27 workspace move. Kept in the repo so the transformation
is auditable rather than a silent sed over 17 files.

For every `.py` under tools/mvpw-mcp/ that assigns a module-level DEPLOY or DEPLOY_DIR
to a literal absolute path, this:

  1. backs the file up next to itself as `<name>.pre-paths.bak`
  2. rewrites the assignment to call `paths.deploy_dir()`
  3. ensures `import paths` exists (they all already sit in the same directory, and
     every one of them already does sys.path juggling or is run from there)
  4. reports what changed

Idempotent: a file that already calls deploy_dir() is left alone.

Run:  python tools/mvpw-mcp/_migrate_paths.py [--dry-run]
"""
from __future__ import annotations

import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PATTERN = re.compile(
    r'^(?P<var>DEPLOY|DEPLOY_DIR)\s*=\s*r?"(?P<path>[A-Za-z]:\\[^"]*?)"\s*$',
    re.MULTILINE)


def rewrite(text: str) -> tuple[str, list[str]]:
    """Return (new_text, [what changed])."""
    changes: list[str] = []

    def sub(m: re.Match) -> str:
        var, path = m.group("var"), m.group("path")
        changes.append("%s = %r -> paths.deploy_dir()" % (var, path))
        return '%s = paths.deploy_dir()' % var

    new = PATTERN.sub(sub, text)
    if not changes:
        return text, []

    # Ensure `import paths` is present. Put it right after the last stdlib import at
    # the top so it reads naturally, and never after the DEPLOY assignment (which
    # would be a NameError).
    if not re.search(r'^import paths\b', new, re.MULTILINE):
        lines = new.split("\n")
        insert_at = 0
        for i, line in enumerate(lines[:60]):
            if re.match(r'^(import |from )\w', line):
                insert_at = i + 1
        lines.insert(insert_at, "import paths")
        new = "\n".join(lines)
        changes.append("added `import paths`")

    return new, changes


def main() -> int:
    dry = "--dry-run" in sys.argv
    targets: list[str] = []
    for name in sorted(os.listdir(HERE)):
        if not name.endswith(".py"):
            continue
        if name in ("paths.py", "_migrate_paths.py"):
            continue
        path = os.path.join(HERE, name)
        with open(path, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        new, changes = rewrite(text)
        if not changes:
            continue
        targets.append(name)
        print("%s" % name)
        for c in changes:
            print("    %s" % c)
        if not dry:
            shutil.copy2(path, path + ".pre-paths.bak")
            with open(path, "w", encoding="utf-8", newline="") as fh:
                fh.write(new)

    print()
    if not targets:
        print("nothing to do -- every file already resolves through paths.py")
        return 0
    print("%s %d file(s)" % ("would rewrite" if dry else "rewrote", len(targets)))
    if not dry:
        print("backups written as <name>.pre-paths.bak next to each file")
    return 0


if __name__ == "__main__":
    sys.exit(main())
