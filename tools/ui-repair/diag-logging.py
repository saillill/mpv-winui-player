"""Why has the app not written a log since 2026-09-25?

Established so far, each measured rather than assumed:

  * a launch today writes `display-info.log` and no `mpv-winui.<date>.log.txt`;
  * the setting is not the cause -- `EnableDebugLog=1` in the registry (verified by reading
    it back) and a relaunch still produces no log file;
  * the code path looks right: `AppContext.Init` calls `LoggerHelper.SetupLogger()` first,
    and `ApplyLogLevel` calls `NLog.LogManager.Setup().LoadConfiguration(builder => ...)`
    with a `WriteToFile` target under `%LOCALAPPDATA%\mpv-winui\logs`;
  * the target path comes from `AppData.Current.ResolveLocalData(...)`, which dereferences
    `Lazy<StorageFolder>` -> `ApplicationData.GetForUnpackaged(publisher, product)`.

The remaining suspect is that last link: a WinRT call made from `Init`, before the window
exists and possibly before the process is COM-ready. If it throws, the whole
`LoadConfiguration` callback throws, the logger stays unconfigured, and every
`AppLogger.*` call goes nowhere -- silently, because nothing can log that logging failed.

This checks whether the call actually works in this process shape, and whether the
directory it names is the one NLog would write to.
"""
import os
import sys

LOCAL = os.environ.get("LOCALAPPDATA", "")
EXPECTED_DIR = os.path.join(LOCAL, "mpv-winui", "logs")
PUBLISHER = "mpv-winui"
PRODUCT = "mpv-winui"


def ps(code):
    import subprocess
    r = subprocess.run(["pwsh", "-NoProfile", "-Command", code],
                       capture_output=True, text=True, errors="replace")
    return (r.stdout or "").strip(), (r.stderr or "").strip()


def main():
    print("expected log directory: %s" % EXPECTED_DIR)
    print("  exists:", os.path.isdir(EXPECTED_DIR))
    if os.path.isdir(EXPECTED_DIR):
        newest = sorted(
            (f for f in os.listdir(EXPECTED_DIR)),
            key=lambda f: os.path.getmtime(os.path.join(EXPECTED_DIR, f)),
            reverse=True)
        print("  newest file:", newest[0] if newest else "(empty)")

    # What the app's own ResolveLocalData would answer. GetForUnpackaged builds
    # <LocalAppData>\<publisher>\<product> for unpackaged apps, NOT <LocalAppData>\mpv-winui.
    guessed = os.path.join(LOCAL, PUBLISHER, PRODUCT, "logs")
    print("\nwhat GetForUnpackaged(%r, %r) would resolve to:" % (PUBLISHER, PRODUCT))
    print("  %s" % guessed)
    print("  exists:", os.path.isdir(guessed))
    if os.path.isdir(guessed):
        files = sorted(os.listdir(guessed),
                       key=lambda f: os.path.getmtime(os.path.join(guessed, f)),
                       reverse=True)[:5]
        print("  contains:", files)

    # And the same question through the WinRT surface the app uses.
    out, err = ps(
        "[void][System.Reflection.Assembly]::LoadWithPartialName('Microsoft.WindowsAppSDK');"
        "try {"
        "  $ad = [Microsoft.Windows.Storage.ApplicationData]::GetForUnpackaged('%s','%s');"
        "  'LocalPath: ' + $ad.LocalPath"
        "} catch { 'THREW: ' + $_.Exception.Message }"
        % (PUBLISHER, PRODUCT))
    print("\nGetForUnpackaged through WinRT:")
    print("  " + (out or "(no output)"))
    if err:
        print("  stderr: " + err[:200])

    print("\nverdict:")
    if os.path.isdir(EXPECTED_DIR) and not os.path.isdir(guessed):
        print("  The app writes to %s" % EXPECTED_DIR)
        print("  but GetForUnpackaged resolves elsewhere -> the FileTarget is created under")
        print("  a path the app never lists, OR the call throws and logging dies.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
