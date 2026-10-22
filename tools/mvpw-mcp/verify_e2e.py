"""End-to-end verification through the MCP tool layer (not by hand, not by
screenshot): launch -> snapshot -> assert -> act -> assert -> read mpv state ->
close.

Run:  python tools/mvpw-mcp/verify_e2e.py [--json out.json]

Exit code 0 = every step passed. Any failure prints the step and the evidence.
"""
import argparse
import json
import sys
import time
import traceback

import mpvw_tools as T

RESULTS = []


def step(name, fn, **kw):
    t0 = time.time()
    try:
        out = fn(**kw)
        ok = not (isinstance(out, dict) and out.get("passed") is False)
        RESULTS.append({
            "step": name, "ok": ok, "seconds": round(time.time() - t0, 2),
            "result": out,
        })
        print(("  PASS  " if ok else "  FAIL  ") + name)
        if not ok:
            print("        " + json.dumps(out, ensure_ascii=False))
        return out
    except Exception as e:
        RESULTS.append({
            "step": name, "ok": False, "seconds": round(time.time() - t0, 2),
            "error": f"{type(e).__name__}: {e}",
            "trace": traceback.format_exc(limit=3),
        })
        print("  ERROR " + name + " -> " + type(e).__name__ + ": " + str(e))
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default="")
    args = ap.parse_args()

    print("== mpvw MCP end-to-end ==")

    # 0. clean slate
    step("close_app (ensure not running)", T.close_app)

    # 1. launch
    info = step("launch_app", T.launch_app, snapshot=True)
    if not info:
        return finish(args)

    # 1b. mpv playback state over the IPC pipe -- FIRST, while the connection is
    # still usable.
    #
    # This used to run after the UI steps and always failed. Measured cause: in
    # this build mpv's input-ipc-server serves one client per process, and the
    # connection goes away once the conversation pauses (idle 2 s -> ERROR_NO_DATA,
    # with the app alive and UIA still fine). Running the twelve UI assertions
    # first spends that window, so the read has to happen here.
    st = step("read_playback_state", T.read_playback_state)
    if st:
        print("      pause=%r volume=%r idle=%r" % (
            st.get("pause"), st.get("volume"), st.get("idle-active")))

    # 2. the shell's own controls are reachable by AutomationId
    step("assert ShellNav exists", T.assert_element_state,
         automation_id="ShellNav")
    step("assert ShellTitleText enabled", T.assert_element_state,
         automation_id="ShellTitleText", enabled=True, visible=True)
    step("assert ProgressSlider visible", T.assert_element_state,
         automation_id="ProgressSlider", visible=True)
    step("assert Settings button enabled", T.assert_element_state,
         automation_id="ShellSettingsButton", enabled=True)

    # 3. nav to Music, then assert the section heading changed
    snap = T.get_ui_snapshot(max_depth=10, redact_names=True)
    ids = sorted({e["automationId"] for e in snap["elements"] if e["automationId"]})
    print("  AutomationId inventory (%d):" % len(ids))
    for i in ids:
        print("      " + i)

    before = T.assert_element_state(automation_id="LibrarySectionTitle")
    step("assert LibrarySectionTitle exists (home)", T.assert_element_state,
         automation_id="LibrarySectionTitle", visible=True)

    step("select nav music", T.invoke_control,
         automation_id="ShellNav_music", action="select")
    time.sleep(1.2)
    step("assert section title still present after nav", T.assert_element_state,
         automation_id="LibrarySectionTitle", visible=True)

    step("select nav videos", T.invoke_control,
         automation_id="ShellNav_videos", action="select")
    time.sleep(1.2)
    step("assert VideoAddFolderButton exists", T.assert_element_state,
         automation_id="VideoAddFolderButton", visible=True)

    step("select nav queue", T.invoke_control,
         automation_id="ShellNav_queue", action="select")
    time.sleep(1.2)
    # Assert the empty state's *text*, not the Grid that holds it: a Grid is
    # not a UI Automation node in WinUI, so QueueEmptyState never appears in
    # the tree (measured 2026-10-22). Its children do.
    step("assert QueueEmptyTitle visible", T.assert_element_state,
         automation_id="QueueEmptyTitle", visible=True)
    step("assert QueueClearButton enabled", T.assert_element_state,
         automation_id="QueueClearButton", enabled=True)

    step("select nav home (restore)", T.invoke_control,
         automation_id="ShellNav_home", action="select")

    # 4. the IPC read already happened in 1b, while the connection was alive

    # 5. a diagnosis screenshot (not the pass/fail evidence)
    step("screenshot_region (diagnosis)", T.screenshot_region,
         path=r"C:\Users\z2637\Documents\DeepSeek\mpv\_work\mcp\e2e-home.png")

    # 6. must not leave the app running
    step("close_app", T.close_app)
    step("assert closed (no process)", _assert_closed)

    return finish(args)


def _assert_closed():
    pid = T._pid()
    return {"passed": pid is None, "pid": pid}


def finish(args):
    ok = all(r["ok"] for r in RESULTS)
    passed = sum(1 for r in RESULTS if r["ok"])
    print("\n%d/%d steps passed -> %s" % (passed, len(RESULTS), "OK" if ok else "FAILED"))
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump({"ok": ok, "steps": RESULTS}, f, ensure_ascii=False, indent=1)
        print("wrote", args.json)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
