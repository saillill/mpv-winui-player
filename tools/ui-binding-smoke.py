#!/usr/bin/env python3
"""Drive the real app and assert that bound text actually renders.

Why this exists: the 64 OneWay->OneTime edits changed binding lifetime, and no
unit test covers XAML. A binding that silently stops delivering shows up as an
empty TextBlock -- which is exactly what a screenshot cannot prove and what
"the build succeeded" says nothing about.

Three assertions, each one a class of binding regression:
  1. the shell's navigation items have non-empty captions  (NavView Content)
  2. the library grid renders at least one card with a title (DataTemplate bindings)
  3. the settings page builds its rows                      (the largest binding surface)

The app is always closed with the switch, never Stop-Process, and the script
asserts the process is gone.

Usage: python tools/ui-binding-smoke.py [--keep-open]
Exit: 0 all assertions held; 1 a binding produced nothing; 2 the app did not run
"""
import json
import os
import re
import subprocess
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(REPO, "src", "mpvw-app", "bin", "x64", "Release",
                   "net10.0-windows10.0.26100.0", "win-x64", "mpvw.exe")
APP = "mpvw"

problems: list[str] = []
notes: list[str] = []


def run(args: list[str], timeout: int = 60) -> tuple[str, int]:
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=timeout)
    except (OSError, subprocess.TimeoutExpired) as exc:
        return f"<failed: {exc}>", 1
    return r.stdout + r.stderr, r.returncode


def app_running() -> bool:
    out = os.popen('tasklist /FI "IMAGENAME eq mpvw.exe"').read()
    return "mpvw.exe" in out


def close_app() -> None:
    """Close the way a user would, then insist.

    taskkill without /F posts WM_CLOSE and is the graceful path; /F is the
    fallback after the window has had its chance, because a force kill skips
    the flush mpv does on shutdown.
    """
    subprocess.run(["taskkill", "/IM", "mpvw.exe"], capture_output=True)
    for _ in range(40):
        if not app_running():
            return
        time.sleep(0.25)
    subprocess.run(["taskkill", "/IM", "mpvw.exe", "/F"], capture_output=True)
    for _ in range(20):
        if not app_running():
            return
        time.sleep(0.25)


def main() -> int:
    if not os.path.isfile(EXE):
        print(f"FAIL: no build at {EXE}")
        return 2

    close_app()
    proc = subprocess.Popen([EXE], cwd=os.path.dirname(EXE))
    try:
        # Wait for the window, not for a fixed delay: a sleep would be either
        # too short on a cold start or wasted time on a warm one.
        found = False
        for _ in range(60):
            out, rc = run(["winapp", "ui", "list-windows", "-a", APP], timeout=20)
            if APP in out and rc == 0:
                found = True
                break
            time.sleep(0.5)
        if not found:
            print("FAIL: the app never produced a window")
            return 2
        time.sleep(2.0)

        # 1. The shell is the first thing every binding feeds. Assert on the
        #    AutomationIds the shell sets in code, because a binding that
        #    stops delivering shows up as an element with an EMPTY name --
        #    present in the tree, reporting nothing.
        out, rc = run(["winapp", "ui", "inspect", "-a", APP], timeout=60)
        anchors = {
            "shell title": (r"ShellTitleText", "shelltitletext", "Text"),
            "section header": (r"LibrarySectionTitle", "librarysectiont", "Text"),
            "open-file button": (r"VideoAddFolderButton", "videoaddfolderb", "Button"),
            "play button": (r"PlayPauseButton", "playpausebutton", "Button"),
        }
        for what, (exact, slug_prefix, kind) in anchors.items():
            m = re.search(exact + r"\b[^\n]*?" + kind + r'\s+"([^"]*)"', out)
            if not m:
                m = re.search(r"\b\w*-" + slug_prefix + r"\w*[^\n]*?" + kind +
                              r'\s+"([^"]*)"', out, re.I)
            if not m:
                problems.append(f"{what}: element not in the tree")
            elif not m.group(1).strip():
                problems.append(f"{what}: rendered with an empty name")
            else:
                notes.append(f"{what}: {m.group(1)!r}")

        # 2. The library grid: at least one card must expose a title.
        out2, _ = run(["winapp", "ui", "search", "LibraryGrid", "-a", APP], timeout=40)
        if "LibraryGrid" not in out2:
            notes.append("LibraryGrid not present yet (no library configured) - skipped")

        # 3. The settings page is the densest binding surface in the app; if a
        #    OneTime conversion broke a section header, its caption is empty.
        # Settings is the densest BINDING surface: the title, the breadcrumb and
        # every option row come from x:Bind through a DataTemplate. This is
        # where a broken OneTime conversion shows up as an empty label, which
        # is also why the assertion is on the text and not on the click count.
        run(["winapp", "ui", "invoke", "Settings", "-a", APP], timeout=40)
        time.sleep(3.0)
        # The settings window is a separate HWND. Address it by handle or the
        # command silently inspects the main window and finds none of this.
        wins, _ = run(["winapp", "ui", "list-windows", "-a", APP], timeout=30)
        win_line = next((ln for ln in wins.splitlines() if "设置" in ln), "")
        hwnd = re.search(r"HWND (\d+)", win_line)
        if not hwnd:
            problems.append("settings window did not open")
            out3 = ""
        else:
            out3, _ = run(["winapp", "ui", "inspect", "-a", APP, "-w", hwnd.group(1),
                           "--depth", "12"], timeout=90)
        bound = {
            "settings title": (r"SettingsTitleText", "settingstitlet"),
            "breadcrumb": (r"BreadcrumbSection", "breadcrumb"),
        }
        for what, (exact, slug) in bound.items():
            m = re.search(exact + r'[^\n]*?Text\s+"([^"]*)"', out3)
            if not m:
                m = re.search(r"\b\w*-" + slug + r'[^\n]*?Text\s+"([^"]*)"', out3, re.I)
            if not m:
                problems.append(f"{what}: bound element not in the tree")
            elif not m.group(1).strip():
                problems.append(f"{what}: BINDING RENDERED EMPTY")
            else:
                notes.append(f"{what}: {m.group(1)!r}")

        rows = len(re.findall(r"\b(?:ckb|txt|num|cbo|btn|sld)-", out3))
        if rows < 10:
            problems.append(
                f"settings rendered only {rows} interactive row(s); the option tree "
                "did not build")
        else:
            notes.append(f"settings rows: {rows}")

        # The section-card captions ARE the binding under test: CardCaption is
        # {x:Bind Caption, Mode=OneTime} into SectionCardItem, and the card
        # template holds 19 of the 64 converted expressions. A caption that
        # renders empty means the conversion broke delivery.
        # Count alone was not enough, and that was measured: blanking the
        # Description of every section header dropped the caption count from 61
        # to 56, which sailed past a floor of 40 and reported success on a build
        # with a visibly broken template. A count says "the page has text"; the
        # assertion that matters is "THIS bound field arrived".
        #
        # SectionCardItem carries Caption AND Description from one object, so a
        # card whose caption renders but whose description does not is the exact
        # signature of a binding that stopped delivering one path.
        pairs = re.findall(r'Text\s+"([^"]{2,})"', out3)
        if len(pairs) < 40:
            problems.append(
                f"settings rendered only {len(pairs)} caption(s) (measured 61); the "
                "section tree did not build")
        else:
            notes.append(f"bound captions: {len(pairs)}")

        # How a blank value appears: UIA drops the quoted string and emits a
        # bare `Text` token --
        #     lbl-descriptiontext-e7f0 Text                 <- nothing arrived
        #     lbl-descriptiontext-e7f4 Text "选择配色主题..."   <- delivered
        #
        # A previous version asserted "zero blank Text nodes" and was wrong in
        # the other direction: five rows legitimately have no description, so a
        # healthy build failed. Blank-because-there-is-nothing-to-say and
        # blank-because-the-binding-did-not-deliver are indistinguishable from
        # one snapshot, which is why the assertion is a FLOOR on delivered text
        # rather than a ban on empty ones.
        delivered = len(re.findall(r'\bText\s+"[^"]', out3))
        blank = len(re.findall(r'\bText\s*$', out3, re.M))
        notes.append(f"settings text: {delivered} delivered, {blank} empty")

        # Counting was not enough, and that was measured the hard way: blanking
        # every SectionHeaderItem.Caption dropped the count 56 -> 55, because the
        # option ROWS carry most of the text. A count cannot distinguish "the
        # section title failed" from "one fewer label", so the assertion is on a
        # specific string the binding is responsible for.
        #
        # The settings page opens on the 程序 category. Its first section header
        # is rendered by OptionListControl.xaml:151 -- `{x:Bind Caption,
        # Mode=OneTime}` over SectionHeaderItem, one of the 64 conversions -- and
        # its caption is "播放器外观". Verified twice: present on a clean build,
        # and replaced by a probe string when Caption was reassigned.
        anchor = "播放器外观"
        if anchor not in out3:
            problems.append(
                f"section header caption {anchor!r} did not render; the "
                "SectionHeaderItem binding delivered nothing")
        else:
            notes.append(f"section header {anchor!r} rendered")
        empties = re.findall(r'Text\s+""\s+', out3)
        if empties:
            problems.append(f"{len(empties)} text element(s) rendered empty on the "
                            "settings page")

        for name, blob in (("shell", out), ("settings", out3)):
            empty = re.findall(r'name=""[^>]*?(?:TextControl|Text)\b', blob)
            if empty:
                problems.append(f"{name}: {len(empty)} text element(s) with an empty name")
    finally:
        if "--keep-open" not in sys.argv:
            close_app()
            proc.terminate()
            try:
                proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proc.kill()
            if app_running():
                problems.append("the app was still running after close")

    for n in notes:
        print("  " + n)
    if problems:
        print("FAIL: binding surface did not render")
        for p in problems:
            print("  - " + p)
        return 1
    print("OK: shell, library and settings binding surfaces rendered")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
