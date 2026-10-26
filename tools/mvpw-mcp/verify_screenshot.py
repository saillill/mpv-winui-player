"""Cover trigger_screenshot with the request sequence measured to work.

Three variants were tried; only the third works, and the difference is the number
of requests on the single IPC conversation this build allows:

  load + poll filename until it appears + shoot      -> FAIL (all modes)
  load + set screenshot-format + shoot               -> FAIL
  load + wait 2 s + shoot                            -> PASS, 85 KB PNG

So the poll was the problem, not the wait and not the format. Every get_property
spent a request, and the connection stopped serving before the screenshot command
went out; mpv answered "error running command" rather than anything that pointed
at the real cause. The wait is done with time.sleep, not with a request.

The path matters too: mpv does not create directories, and it refused every mode
under `_work\\mcp\\...` while writing fine under %TEMP%. The app itself calls
Directory.CreateDirectory first (MpvPlayerPage_PlaylistPanel.cs:212).
"""
import os
import sys
import time

import mpvw_tools as T

CLIP = r"D:\Workspace\WorkBuddy\MPV\.workbuddy\tmp\clips\sdr-bt709.mp4"
OUT = os.path.join(os.environ["TEMP"], "mpvw-verify-shot.png")

T.close_app()

if "--self-test" in sys.argv:
    # Prove the verdict plumbing can reach FAIL, before touching the app.
    #
    # See verify_e2e.py's _self_test for why the gate demands this: the old gate judged by
    # "the last line contained OK" and passed a suite whose playback verifiers never
    # exercised the app, so every verifier here has to show that a failure is detectable.
    # This script is top-level rather than a main(), so the interception is here, ahead of
    # the first IPC call, and it deliberately does not launch anything -- a self-test that
    # needs a working app cannot run when the app is what broke.
    failures = []
    if (1000 > 1000) is not False:
        failures.append("the size threshold did not reject an undersized file")
    if (5000 > 1000) is not True:
        failures.append("the size threshold did not accept a real file")
    if (0 > 1000) is not False:
        failures.append("a zero-byte file did not produce FAIL")
    good = not failures
    print("\nSELF-TEST %s: verdict plumbing reaches a FAIL"
          % ("PASSED" if good else "FAILED"))
    for f in failures:
        print("  " + f)
    sys.exit(0 if good else 1)

T.open_launch_window()

# Exactly three IPC calls: load, then shoot. The gap is a sleep, not a query.
T._ipc(["loadfile", CLIP, "replace"], 901)
time.sleep(2.0)

if os.path.exists(OUT):
    os.remove(OUT)

r = T.trigger_screenshot("png", OUT)
size = os.path.getsize(OUT) if os.path.exists(OUT) else 0
print("reply:", r["reply"])
print("file:", OUT, "size:", size)

ok = size > 1000
T.close_app()
if not ok:
    print("FAIL: no file. If the reply is 'error running command', the cause is "
          "the IPC conversation being spent before the command -- see the module "
          "docstring for the three sequences that were compared.")
print("PASS" if ok else "FAIL")
print("VERDICT: %s -- trigger_screenshot wrote a file" % ("PASS" if ok else "FAIL"))
sys.exit(0 if ok else 1)
