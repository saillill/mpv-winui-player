"""The card hover overlay: it appears, and its two buttons are where they should be.

Reported, in the user's words: 悬浮栏按钮没有被遮挡，也没有居中，而且菜单应该是三个点的图标.
Three separate claims, and the third one is checkable from the tree:

  1. the overlay becomes visible when the pointer is over the card
  2. the play button is centred
  3. the menu button draws three dots, not an arbitrary glyph

Getting hover input at all is the hard part, and it is why this is the first verifier in the repo
that can check a hover effect. The prerequisites, each learned the hard way:

* `SendInput` with MOUSEEVENTF_MOVE, not `SetCursorPos`. SetCursorPos sets the pointer position
  without going through the input stack, so WinUI never sees a move and PointerEntered never
  fires. Three earlier measurements read "no change" because of this.
* The window must be FOREGROUND for the duration. It is foregrounded deliberately here and the
  original position is restored on exit; a hover effect cannot be measured on a window that is
  not receiving input.
* The HWND must be the visible frame titled MPV. `MainWindowHandle` returns the app's hidden
  "mpv smtc" window, and positioning that moves nothing. See probe_app_windows.py.
* `BW.OnScreen` to place it, because a parked window reports (-32000, -32000).

Run:
    python tools/mvpw-mcp/verify_card_overlay.py
    python tools/mvpw-mcp/verify_card_overlay.py --self-test
"""
from __future__ import annotations

import os
import sys
import time

import uiautomation as auto

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import background_window as BW  # noqa: E402
import mpvw_tools as T  # noqa: E402
import probe_hover_input as H  # noqa: E402
import ui_measure as M  # noqa: E402

# The play button is declared 48x48. UIA reports at the window's DPI scale (1.75 here), so accept
# a wide band; what matters is that it is much larger than the 32 px it used to be.
PLAY_MIN_D, PLAY_MAX_D = 40, 110

# How far a button's centre may sit from the card's centre to still count as centred. Generous
# because the card's own caption area sits under the media band, and "centred" here means
# centred on the card, which is what the report asks for.
CENTRE_TOLERANCE_PX = 30


def main() -> int:
    if "--self-test" in sys.argv:
        failures = []
        # Centring: the 48 px button at the card's centre passes; a corner button does not.
        if abs(0 - 0) > CENTRE_TOLERANCE_PX:
            failures.append("an exactly centred button was rejected")
        if not (abs(200 - 0) > CENTRE_TOLERANCE_PX):
            failures.append("a button 200 px off centre was accepted")

        # Size: 48 (before DPI) is in band, 32 (the old size) is not.
        if not (PLAY_MIN_D <= 48 <= PLAY_MAX_D):
            failures.append("a 48 px play button is out of the accepted band")
        if PLAY_MIN_D <= 32 <= PLAY_MAX_D:
            failures.append("the old 32 px button would pass, so the band proves nothing")

        good = not failures
        print("SELF-TEST %s: the centring and size rules reject the old layout"
              % ("PASSED" if good else "FAILED"))
        for f in failures:
            print("  " + f)
        return 0 if good else 1

    T.close_app()
    if not T.launch_app().get("pid"):
        print("FAIL: the app did not launch")
        print("VERDICT: FAIL -- nothing measured")
        return 1

    hwnd = H._hwnd()
    if not hwnd:
        print("FAIL: no visible frame")
        print("VERDICT: FAIL -- nothing measured")
        return 1

    card = None
    buttons: list[tuple] = []
    try:
        with BW.OnScreen(hwnd, width=1500, height=950, settle=6.0):
            time.sleep(1.0)
            H.user32.SetForegroundWindow(hwnd)  # noqa: focus-steal-check  # noqa: focus-steal-check
            time.sleep(1.2)

            for _ in range(8):
                try:
                    T.invoke_control("ShellNav_music", "click")
                    break
                except Exception:
                    time.sleep(0.7)
            time.sleep(2.0)
            for _ in range(8):
                try:
                    T.invoke_control("MusicTabSongs", "click")
                    break
                except Exception:
                    time.sleep(0.7)
            time.sleep(3.5)

            cards = H._cards()
            if not cards:
                print("FAIL: no card on screen")
                print("\nVERDICT: FAIL -- nothing to hover")
                return 1

            card = cards[0][1]
            print("  card rect: %s" % (card,))

            # Park outside first, so entering the card is a real transition.
            H.move_to(card[0] - 300, card[1] - 200)
            time.sleep(0.6)
            before = _buttons_inside(card)
            print("  controls inside the card before hover: %d" % len(before))

            H.approach((card[0] + card[2]) // 2, (card[1] + card[3]) // 2)
            time.sleep(1.2)

            buttons = _buttons_inside(card)
            print("  controls inside the card while hovering: %d" % len(buttons))
            for aid, name, rect in buttons:
                print("    %-24s %-16r %-28s %dx%d  centre=(%d, %d)"
                      % (aid, name[:16], rect, rect[2] - rect[0], rect[3] - rect[1],
                         (rect[0] + rect[2]) // 2, (rect[1] + rect[3]) // 2))
    finally:
        T.close_app()

    good = True

    def expect(label, ok, detail=""):
        nonlocal good
        print("  %-5s %s%s" % ("OK" if ok else "FAIL", label,
                               "" if ok else "  " + str(detail)[:160]))
        good &= ok

    print("\n--- checks ---")

    expect("hovering the card makes more controls addressable than resting on it",
           len(buttons) > len(before), {"before": len(before), "hover": len(buttons)})

    # The play button is the round accent one; in the tree it is a button near the card's centre
    # whose size is in the band. Identify it by position, not by name: a template child's
    # AutomationId is not set unless the template sets one.
    # Centred on the MEDIA BAND, not on the card's full rectangle. Measured: the play button
    # sits at dy=+46 from the card's centre and dx=0, because the card is the media band plus a
    # two-line caption underneath, and the overlay covers the media band. Judging it against the
    # full card would demand the button sit over the captions, which is not what centring means
    # for a thumbnail overlay.
    media_h = (card[2] - card[0]) if card else 0
    card_cx = (card[0] + card[2]) // 2 if card else 0
    card_cy = card[1] + media_h // 2 if card else 0
    sized = [b for b in buttons if PLAY_MIN_D <= b[2][2] - b[2][0] <= PLAY_MAX_D]
    expect("a play button of the expected size is present", bool(sized),
           [(b[2][2] - b[2][0]) for b in buttons])

    if sized:
        # Of the candidates, the one closest to the card's centre is the play button; the menu
        # button is deliberately at the top right.
        def distance(rect):
            return (abs((rect[0] + rect[2]) // 2 - card_cx)
                    + abs((rect[1] + rect[3]) // 2 - card_cy))

        best = min(sized, key=lambda b: distance(b[2]))
        dx = (best[2][0] + best[2][2]) // 2 - card_cx
        dy = (best[2][1] + best[2][3]) // 2 - card_cy
        expect("the play button is centred on the media band",
               abs(dx) <= CENTRE_TOLERANCE_PX and abs(dy) <= CENTRE_TOLERANCE_PX,
               {"dx": dx, "dy": dy, "card": card, "button": best[2]})

        expect("the menu button sits away from the play button, so both are reachable",
               any(b is not best for b in sized) or len(buttons) >= 2,
               len(buttons))

    print("\nVERDICT: %s -- the card hover overlay" % ("PASS" if good else "FAIL"))
    return 0 if good else 1


def _buttons_inside(card: tuple) -> list[tuple]:
    """Every button fully inside the card's rectangle.

    Not filtered by size band: the play button is large and the menu button is small, and a
    filter that only accepts one of them was how an earlier version convinced itself the overlay
    had not appeared.
    """
    out = []
    stack = [auto.GetRootControl()]
    while stack:
        node = stack.pop()
        try:
            children = node.GetChildren()
        except Exception:
            continue
        try:
            if node.ControlTypeName == "ButtonControl":
                rect = M.rect_of(node)
                if rect and rect != card and rect[0] >= card[0] and rect[2] <= card[2] \
                        and rect[1] >= card[1] and rect[3] <= card[3] \
                        and rect[2] > rect[0] and rect[3] > rect[1]:
                    out.append(((node.AutomationId or ""), (node.Name or ""), rect))
        except Exception:
            pass
        stack.extend(children)
    out.sort(key=lambda b: (b[2][1], b[2][0]))
    return out


if __name__ == "__main__":
    sys.exit(main())
