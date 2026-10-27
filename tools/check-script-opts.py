#!/usr/bin/env python3
"""Every Lua script in scripts/ that shadows an mpv built-in must still accept
the script-opts keys mpv ships for it.

Why this exists. `mpv.conf` grew this line: it appears verbatim inside the mpv
binary as

    script-opt=select-populate_menu_data=no

mpv's own copy of select.lua declares five script options; the project's
localized copy declares two. So at every launch mpv hands select.lua a key it
does not know, and the script logs "unknown key populate_menu_data, ignoring".
Nothing breaks -- the key is simply dropped -- which is exactly why it stayed
unnoticed: a warning in a log nobody read, about an option nobody needed.

The general shape is worth a checker because it does not stay harmless:
`mpv.conf`'s built-in profile keeps naming options for the bundled script, and
if a future option there IS load-bearing, the same silent drop becomes a
behaviour regression. The invariant is narrow and mechanical:

    for every local script that also ships inside mpv-2.dll,
    every `script-opt=<name>-<key>=` the binary hands that script
    must appear in the local copy's `local options = { ... }` table.

Source of the "what mpv hands it" side: the binary is the authority, since that
is what actually runs. tools/fetch-mpv-libs.ps1 pins the exact mpv build (and
verifies its sha256), so the strings in it are a stable, reviewable input rather
than whatever happens to be installed.

Source of the "what the local copy accepts" side: the `local options = {}` table
in the file. That table is the only thing mp.options.read_options consults.

Exit code 1 when a local copy rejects a key mpv sends for it.
"""

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SCRIPTS = REPO / "data" / "scripts"
DEFAULT_DLL = REPO / "src" / "libs" / "bin" / "x64" / "mpv-2.dll"


def builtin_script_opts(dll: Path) -> dict[str, set[str]]:
    """{script-name: {keys}} for every script-opt the binary hardcodes.

    The binary carries mpv's default profiles as one text blob; the relevant
    lines look like `script-opt=select-populate_menu_data=no`. The script name
    may itself contain a hyphen, so the split is on the LAST hyphen before the
    key: mpv documents `<script>-<key>=<value>` and keys never contain hyphens
    (they are Lua identifiers), which makes the last hyphen unambiguous.
    """
    blob = dll.read_bytes()
    found: dict[str, set[str]] = {}
    for m in re.finditer(rb"script-opt=([a-z0-9_]+)-([a-z0-9_]+)=", blob):
        name = m.group(1).decode()
        key = m.group(2).decode()
        found.setdefault(name, set()).add(key)
    return found


def declared_options(lua: Path) -> set[str]:
    """Keys of the table the script hands to read_options(...).

    Not `local options = {` -- that was the first attempt and it reported three
    files as declaring nothing. They use different names for the same thing
    (`o = {`, `script_opts = {`), and the only structural fact that matters is
    which table is passed to read_options. So: find the call, take its first
    argument, resolve that identifier to its `local <name> = { ... }` table.
    """
    text = lua.read_text(encoding="utf-8", errors="replace")

    m = re.search(r"read_options\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)", text)
    if m:
        var = m.group(1)
        table = re.search(rf"local\s+{re.escape(var)}\s*=\s*\{{", text)
    else:
        # No read_options call: fall back to the conventional name so a script
        # that reads opts some third way is still inspected rather than skipped.
        table = re.search(r"local\s+options\s*=\s*\{", text)

    if not table:
        return set()

    i = text.index("{", table.start())
    depth = 0
    for j in range(i, len(text)):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                break
    body = text[i + 1:j]
    # `key = value,` at any indentation; a nested table would be a value, not a key.
    return set(re.findall(r"^\s*([a-z0-9_]+)\s*=", body, re.M))


def local_script_names() -> dict[str, Path]:
    return {p.stem: p for p in sorted(SCRIPTS.glob("*.lua"))}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", default=str(DEFAULT_DLL),
                        help="mpv-2.dll to read the built-in script-opts from")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    dll = Path(args.dll)
    if not dll.exists():
        print(f"ERROR: mpv library not found at {dll}\n"
              f"       run tools/fetch-mpv-libs.ps1 first", file=sys.stderr)
        return 2

    sent = builtin_script_opts(dll)
    if not sent:
        # A silent zero here would mean the regex stopped matching the binary's
        # layout, not that mpv sends nothing. Refuse rather than pass.
        print("ERROR: found no script-opt lines in the mpv library -- the "
              "extraction regex no longer matches this build", file=sys.stderr)
        return 2

    local = local_script_names()
    shadowing = sorted(set(sent) & set(local))

    errors: list[str] = []
    print(f"scripts in repo            : {len(local)}")
    print(f"scripts mpv sends opts for : {len(sent)}")
    print(f"shadowed by a local copy   : {len(shadowing)}"
          + (f" ({', '.join(shadowing)})" if shadowing else ""))

    for name in shadowing:
        provided = declared_options(local[name])
        missing = sorted(sent[name] - provided)
        extra = sorted(provided - sent[name])
        if args.verbose or missing:
            print(f"\n  {name}.lua")
            print(f"    mpv sends   : {', '.join(sorted(sent[name]))}")
            print(f"    copy accepts: {', '.join(sorted(provided)) or '(none)'}")
            if missing:
                print(f"    NOT ACCEPTED: {', '.join(missing)}")
            if extra:
                print(f"    local-only  : {', '.join(extra)}  (fine: an option "
                      f"the old copy had, or a project addition)")
        if missing:
            errors.append(
                f"{name}.lua does not accept {len(missing)} key(s) mpv sends it: "
                f"{', '.join(missing)}")

    print()
    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        print("\nA shadowed script must declare every key mpv hands it, or mpv's "
              "value is silently dropped.\nAdd the missing key to the file's "
              "`local options = { }` table (with the same default mpv uses), or "
              "remove the script-opt line from the config layer if the option is "
              "genuinely unwanted.", file=sys.stderr)
        return 1

    print("OK: every shadowed script accepts the script-opts mpv sends it")
    return 0


if __name__ == "__main__":
    sys.exit(main())
