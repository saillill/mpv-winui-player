#!/usr/bin/env python3
"""Every IDL event the app subscribes to must actually be raised by the native
component, and every event the component raises should mean something.

Why this exists. Two real defects in this repo had the same shape and were
found by hand, twice, with a grep and some guessing:

  * PlayerControl subscribed to `PlaybackFailed`; c49b461 renamed the raise to
    m_fileFailedEvent and left the old declaration behind. The subscription
    compiled, type-checked, and never fired -- so the Error visual state was
    unreachable for any file that failed to load.
  * MpvPlayerPage subscribed to `VoConfigured`; the same refactor renamed the
    raise to m_swapChainChangedEvent. The handler could not run.

In C++/WinRT three separate things make an event: the IDL declares it, the
.cpp implements its accessor pair, and a third site calls m_*Event() to raise
it. Checking one of those and concluding about all three is how both defects
survived. This checker reads all three sources, so the answer is a join rather
than an assumption:

    IDL events  x  raise sites in *.cpp  x  subscribers in *.cs

It reports, per event, which of the three legs exist. It FAILS when an event
the app subscribes to is never raised -- that is the defect above, and it is
not a style question.

It does NOT fail on a declared-but-never-raised event nobody subscribes to.
Those are public surface on a package that ships as a NuGet projection
(mpv-winrt-projection), and removing them is an ABI decision, not a cleanup.
They are reported so the next person sees the state instead of rediscovering
it.
"""

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
NATIVE = REPO / "mpv-winui" / "mpv-winrt"
APP = REPO / "mpv-winui" / "mpv-winui"


def idl_events(src: Path) -> list[str]:
    """Event names declared in the runtime class's IDL."""
    text = src.read_text(encoding="utf-8", errors="replace")
    return re.findall(r"^\s*event\s+\w+\s+(\w+)\s*;", text, re.M)


def raise_sites(src: Path) -> dict[str, list[str]]:
    """{m_xxxEvent: ["file:line", ...]} for every raise in the native sources."""
    found: dict[str, list[str]] = {}
    for f in sorted(src.rglob("*.cpp")):
        text = f.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"(m_\w+Event)\s*\(", text):
            line = text[:m.start()].count("\n") + 1
            found.setdefault(m.group(1), []).append(f"{f.name}:{line}")
    return found


def event_members(src: Path) -> tuple[dict[str, str], set[str]]:
    """Event name -> backing member, plus the set of declared members.

    Two declaration styles coexist: the MPVWINRT_EVENT macro (which builds the
    member name by token pasting, m_##field##Event -- invisible to any search on
    the event name) and explicit winrt::event<> members. Reading the macro's
    field argument is the only way to see through it.
    """
    header = (src / "MpvPlayer.h").read_text(encoding="utf-8", errors="replace")
    members = set(re.findall(r"winrt::event<[^>]+>\s+(m_\w+Event)\s*;", header))
    by_macro = {ev: "m_" + field + "Event"
                for ev, field in re.findall(r"MPVWINRT_EVENT\((\w+),\s*\w+,\s*(\w+)\)", header)}
    return by_macro, members


def subscribers(src: Path) -> str:
    parts = []
    for f in sorted(src.rglob("*.cs")):
        if "\\obj\\" in str(f) or "\\bin\\" in str(f):
            continue
        parts.append(f.read_text(encoding="utf-8", errors="replace"))
    return "\n".join(parts)


def member_for(event: str, by_macro: dict[str, str], members: set[str]) -> str | None:
    if event in by_macro:
        return by_macro[event]
    guess = "m_" + event[0].lower() + event[1:] + "Event"
    return guess if guess in members else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true",
                        help="list every event, not only the interesting ones")
    args = parser.parse_args()

    idl = NATIVE / "MpvPlayer.idl"
    for path in (idl, NATIVE / "MpvPlayer.h"):
        if not path.exists():
            print(f"ERROR: {path} not found", file=sys.stderr)
            return 2

    events = idl_events(idl)
    if not events:
        # Zero events means the regex stopped matching, not that the IDL has
        # none. Refuse rather than report a clean bill of health.
        print("ERROR: no events parsed from MpvPlayer.idl -- the regex no longer "
              "matches this file", file=sys.stderr)
        return 2

    raises = raise_sites(NATIVE)
    by_macro, members = event_members(NATIVE)
    cs = subscribers(APP)

    rows = []
    for ev in events:
        member = member_for(ev, by_macro, members)
        sites = raises.get(member, []) if member else []
        listened = bool(re.search(rf"\.{ev}\s*\+=", cs))
        rows.append((ev, bool(sites), len(sites), listened))

    unlistened_firing = [r for r in rows if r[1] and not r[3]]
    dead_but_listened = [r for r in rows if not r[1] and r[3]]
    never_raised = [r for r in rows if not r[1]]
    subscribed = [r for r in rows if r[3]]

    print(f"events in IDL        : {len(events)}")
    print(f"subscribed in C#     : {len(subscribed)}")
    print(f"raised by native     : {len([r for r in rows if r[1]])}")
    print(f"raised, nobody listens: {len(unlistened_firing)}"
          + (f" ({', '.join(r[0] for r in unlistened_firing)})" if unlistened_firing else ""))
    print(f"declared, never raised: {len(never_raised)}"
          + (f" ({', '.join(r[0] for r in never_raised)})" if never_raised else ""))

    if args.verbose:
        print()
        print(f"  {'event':30} {'raised':>6} {'sites':>5}  listens")
        for ev, fired, n, listened in rows:
            print(f"  {ev:30} {str(fired):>6} {n:>5}  {listened}")

    errors = []
    if dead_but_listened:
        for ev, _, _, _ in dead_but_listened:
            errors.append(
                f"{ev} is subscribed in C# but never raised by the native component "
                f"-- that handler cannot run")

    print()
    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        print("\nEither wire the raise in MpvPlayer.cpp, or point the subscription "
              "at the event that replaced it.\nThis is the c49b461 failure mode: the "
              "IDL keeps the old name, the accessor still compiles,\nand the "
              "subscription silently never fires.", file=sys.stderr)
        return 1

    if never_raised:
        print("NOTE: declared-but-never-raised events are reported, not failed. "
              "They are public\n      surface on mpv-winrt-projection; removing "
              "them is an ABI change, not a cleanup.")
    print("OK: every subscribed event is raised by the native component")
    return 0


if __name__ == "__main__":
    sys.exit(main())
