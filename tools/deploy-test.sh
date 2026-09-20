#!/usr/bin/env bash
# Deploy the current build into the session's test directory.
#
# Two things this deliberately does that a bare `cp -r` gets wrong (both cost
# real debugging time on 2026-09-18/19):
#
#   1. .xbf must travel WITH the dll. The compiled XAML is a separate file, and
#      dll/xbf mismatch shows up as `InvalidCastException` inside Connect() --
#      which looks exactly like a genuine XAML bug.
#   2. Never delete-and-recreate the destination. The install needs the WinAppSDK
#      runtime DLLs that are already sitting there; wiping the directory leaves a
#      half-copied tree that cannot start at all (the old deploy_full.sh failure
#      mode: 91/588 files, yet it still printed a healthy "xbf count: 42").
#
# So: mkdir -p, then overlay.

set -u

REPO="C:/Users/z2637/.zcode/workspace/default/mpv-winui-player"
SRC="$REPO/mpv-winui/mpv-winui/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64"
DST="C:/Users/z2637/WorkBuddy/2026-09-19-14-59-27/outputs/mpvw-test"

if [ ! -f "$SRC/mpvw.dll" ]; then
    echo "FATAL: no mpvw.dll at $SRC -- build first"
    exit 1
fi
if [ ! -f "$DST/mpvw.exe" ]; then
    echo "FATAL: $DST is not an existing deploy -- refusing to overlay onto nothing"
    exit 1
fi

echo "src dll : $(stat -c '%s bytes %y' "$SRC/mpvw.dll")"
echo "dst dll before: $(stat -c '%s bytes %y' "$DST/mpvw.dll")"

# --- refuse to run as a silent no-op -----------------------------------------
# On Windows, PATH resolves `find` to find.exe (and `stat`/`cp` are not GNU
# either). Windows find answers "FIND: Parameter format not correct" and prints
# nothing, so the copy loop below reads an EMPTY file list and copies nothing --
# while this script still prints its reassuring src/dst lines and ends with a
# bare `xbf count: 0`. It looks like a run and is not one. Measured on
# 2026-09-21: dst dll timestamp unchanged, exit 0.
#
# So probe the toolchain before trusting it, and point at the PowerShell path
# that does work. Use tools/deploy-test.ps1 (native Copy-Item overlay plus a
# verification pass), or tools/compare-deploy.ps1 to see what is stale.
if ! find . -maxdepth 0 -type d >/dev/null 2>&1; then
    echo "FATAL: 'find' here is not GNU find -- this script would silently copy nothing."
    echo "       Use:  pwsh tools/deploy-test.ps1"
    echo "       Or:   pwsh tools/compare-deploy.ps1   (report only)"
    exit 1
fi

mkdir -p "$DST"

# 1) Everything except pdb, mirroring only what the build actually produced.
cd "$SRC" || exit 1
find . -type f ! -name '*.pdb' -print0 | while IFS= read -r -d '' f; do
    d="$DST/$(dirname "$f")"
    mkdir -p "$d"
    cp -f "$f" "$d/"
done

# 2) Prune .xbf that no longer exist in the build. A .xaml that moved or was
#    deleted leaves a stale compiled twin in the deploy dir, and the app loads
#    by resource URI -- "which copy wins" is not something to spend an afternoon
#    on.
cd "$DST" || exit 1
find . -name '*.xbf' -print0 | while IFS= read -r -d '' f; do
    if [ ! -f "$SRC/$f" ]; then
        echo "prune stale xbf: $f"
        rm -f "$f"
    fi
done

echo "dst dll after : $(stat -c '%s bytes %y' "$DST/mpvw.dll")"
echo "xbf count: $(find "$DST" -name '*.xbf' | wc -l)  (expect 42)"
