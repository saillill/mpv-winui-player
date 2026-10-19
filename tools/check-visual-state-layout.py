#!/usr/bin/env python3
"""Catch full-window / full-screen VisualState setters that fight the layout.

Background
----------
MpvPlayerPage.xaml carries a `FullWindow` visual state that swaps the page into
its full-window shape: the menu row and the control row collapse to zero and the
control bar moves up over the video. A `NormalWindow` state with no setters
switches it back.

That state used to also re-dress the playlist toolbar:

    <Setter Target="PlaylistToolBar.HorizontalAlignment" Value="Left" />
    <Setter Target="PlaylistToolBar.Margin" Value="4,-6,8,0" />

Both halves were wrong, and both were wrong in ways that reading the code does
not reveal. The toolbar has `Height="44"` and lives in a `RowDefinition` that is
also `Height="44"`. A -6 top margin therefore asks for 6px more than the row
gives, so the row clips it: the buttons lose their top edge and the sidebar's
first row stops lining up with the video area next to it. That is the bug the
user reported as "全屏下和侧栏间距有问题". Separately, flipping the alignment
moved the playlist buttons to the other end of the pane on a state change that
has nothing to do with them.

Neither symptom is visible without running the app, which is exactly why this
check exists: the repo has no automated UI test, and a magic number added to a
VisualState is invisible to every other checker here.

What is checked
---------------
1. No negative margin in any VisualState setter. A negative margin on a stretch
   element is sometimes legitimate, but on a VisualState setter it is a hand
   adjustment to a geometry problem and should be fixed at the layout instead.
2. No VisualState setter changes the HorizontalAlignment or
   VerticalAlignment of a named element. A state that re-aligns an element the
   state does not own is almost always compensating for something else.

Both are reported as errors, because both are the shape of the bug above rather
than a style preference. Row/column *sizes* and Grid.Row/Column moves are left
alone: collapsing the menu row is the state's entire job.

Exit code 1 on any problem.
"""

import re
import sys

from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
PLAYER = REPO / "mpv-winui" / "mpv-winui" / "Modules" / "Player"

SKIP_DIRS = {"bin", "obj", "packages", ".vs", "node_modules"}

# A Setter in a VisualState block: <Setter Target="X.Y" Value="Z" />. The Target
# carries a property name after the element name; only that suffix is
# interesting, because it is what the state is trying to change.
SETTER = re.compile(
    r'<Setter\s+Target="([^"]+)"\s+Value="([^"]*)"\s*/?>', re.S)

NEGATIVE_MARGIN = re.compile(r"-\d+(?:\.\d+)?")
ALIGNMENT_PROPS = ("HorizontalAlignment", "VerticalAlignment")


def xaml_files():
    for path in sorted(PLAYER.rglob("*.xaml")):
        if SKIP_DIRS & set(path.relative_to(PLAYER).parts):
            continue
        if path.is_file():
            yield path


def visual_state_bodies(text):
    """The bodies of VisualState elements, as (name, body) pairs.

    Brace-free parsing: find each <VisualState ...> opening tag, then scan to
    its matching </VisualState>. VisualStates do not nest in this codebase, so a
    non-greedy match to the next closing tag is exact.
    """
    for match in re.finditer(
            r'<VisualState\s+x:Name="([^"]+)"[^>]*>(.*?)</VisualState>',
            text, re.S):
        yield match.group(1), match.group(2)


def check(files):
    errors = []
    checked_setters = 0

    for path in files:
        rel = path.relative_to(REPO)
        text = path.read_text(encoding="utf-8-sig")

        for state, body in visual_state_bodies(text):
            for target, value in SETTER.findall(body):
                checked_setters += 1
                prop = target.rsplit(".", 1)[-1]

                if prop == "Margin" and NEGATIVE_MARGIN.search(value):
                    line = text[:text.find(f'Target="{target}"')].count("\n") + 1
                    errors.append(
                        f"{rel}:{line}: {state} sets {target}=\"{value}\" -- a "
                        f"negative margin in a visual state is a hand adjustment "
                        f"to a geometry problem; fix the row/column or the "
                        f"element's own layout instead")

                if prop in ALIGNMENT_PROPS:
                    line = text[:text.find(f'Target="{target}"')].count("\n") + 1
                    errors.append(
                        f"{rel}:{line}: {state} sets {target}=\"{value}\" -- a "
                        f"state should not re-align an element it does not own; "
                        f"let the element keep its base alignment")

    return errors, checked_setters, len(files)


def main():
    files = list(xaml_files())
    errors, checked, n_files = check(files)

    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1

    print(f"OK: {n_files} player XAML file(s), {checked} visual-state setter(s), "
          f"no negative margins and no alignment overrides")
    return 0


if __name__ == "__main__":
    sys.exit(main())
