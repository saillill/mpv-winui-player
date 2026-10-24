"""Compare the app's source tree with upstream, by relative path.

The first attempt reported 179 "upstream-only" files, which is every file upstream has --
the comparison stripped a prefix from one side only. Worth recording because it is the
kind of mistake that would have produced a queue full of items that are already done.

After normalising both sides to paths relative to the app project directory, a file is:
  both      present in both  -> candidate for a behaviour diff, not a missing feature
  upstream  only upstream    -> candidate to port
  local     only local       -> candidate to keep (local extension) or to cut
"""
import io
import subprocess
import sys

REPO = r"C:\Users\z2637\Documents\DeepSeek\mpv"
PREFIX = "mpv-winui/mpv-winui/"


def run(*args):
    return subprocess.run(args, cwd=REPO, capture_output=True, text=True,
                          errors="replace").stdout


def norm(paths):
    out = set()
    for p in paths:
        p = p.replace("\\", "/").lower()
        i = p.find(PREFIX)
        if i >= 0:
            p = p[i + len(PREFIX):]
        # The test projects sit under mpv-winui/ but outside the app project.
        if p.startswith(("mpv-conf-test/", "mpv-winrt-test/", "mpv-winrt/")):
            continue
        out.add(p)
    return out


def main():
    up = norm(run("git", "ls-tree", "-r", "--name-only", "FETCH_HEAD").splitlines())
    loc = norm(run("git", "ls-files").splitlines())

    only_up = sorted(up - loc)
    only_loc = sorted(loc - up)
    both = up & loc

    print("upstream app files : %d" % len(up))
    print("local app files    : %d" % len(loc))
    print("in both            : %d" % len(both))
    print("upstream only      : %d" % len(only_up))
    print("local only         : %d" % len(only_loc))

    print("\n--- upstream only (port candidates) ---")
    for p in only_up:
        print("   " + p)

    print("\n--- local only (keep or cut?) ---")
    for p in only_loc:
        print("   " + p)

    out = REPO + r"\_work\upstream-compare.txt"
    io.open(out, "w", encoding="utf-8").write(
        "umbrella\n" + "\n".join([
            "UPSTREAM_ONLY", *("  " + p for p in only_up),
            "", "LOCAL_ONLY", *("  " + p for p in only_loc),
        ]))
    print("\nwritten:", out)


if __name__ == "__main__":
    sys.exit(main())
