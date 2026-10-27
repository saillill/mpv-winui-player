"""Where the workspace keeps things, resolved once instead of hardcoded 17 times.

Why this module exists: every verifier and diagnostic under `tools/mvpw-mcp/` used to
carry its own `DEPLOY = r"C:\\Users\\z2637\\Documents\\DeepSeek\\mpv-deploy"`. That was
correct until 2026-10-27, when the whole project moved out of the shared DeepSeek
workspace into its own. Moving it broke 17 files at once, and would break them again
on the next move.

The layout this resolves against:

    <workspace>/                 <- mpv-project
        mpv-clean/               <- this repo
            tools/mvpw-mcp/      <- this file lives here
        mpv-deploy/              <- the built app runs from here
        mpv-deploy.bak-*/
        mpv.old-*/

So from a file in `tools/mvpw-mcp/`, the repo root is three `dirname` hops up and the
deploy directory is its sibling.

Two ways to override, in priority order:

    MPVW_DEPLOY_DIR      explicit deploy path (use when testing a deploy elsewhere)
    MPVW_WORKSPACE_ROOT  explicit workspace root (use when the tree was copied apart)

Read that order carefully: an explicit deploy path wins over a workspace root, because
a caller who names the deploy directory has said exactly what they mean.

Deliberately NOT guessed: if neither the sibling directory nor an override exists, this
raises instead of falling back to some path that happens to exist. A verifier pointed
at the wrong tree is worse than a verifier that refuses to start -- this repo has
already produced a 16/16 green gate against a tree that had been deleted.
"""
from __future__ import annotations

import os


def repo_root() -> str:
    """The mpv-clean checkout. Derived from this file's location, not hardcoded."""
    return os.path.dirname(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def workspace_root() -> str:
    """The directory that holds mpv-clean and its siblings (deploy, backups)."""
    override = os.environ.get("MPVW_WORKSPACE_ROOT")
    if override:
        return os.path.abspath(override)
    return os.path.dirname(repo_root())


def deploy_dir() -> str:
    """The built app's directory. Raises if it cannot be found.

    Callers that want to *check* rather than *require* should use
    `deploy_dir_or_none()`, so a missing deploy is reported as a failed assertion
    with a clear reason instead of an exception traceback.
    """
    override = os.environ.get("MPVW_DEPLOY_DIR")
    path = os.path.abspath(override) if override else os.path.join(
        workspace_root(), "mpv-deploy")
    if not os.path.isdir(path):
        raise RuntimeError(
            "no deploy directory at %r.\n"
            "  Expected the sibling of the repo root: %r\n"
            "  Override with MPVW_DEPLOY_DIR=<path> if the deploy lives elsewhere."
            % (path, repo_root()))
    return path


def deploy_dir_or_none() -> str | None:
    """Same as `deploy_dir()` but returns None instead of raising."""
    try:
        return deploy_dir()
    except RuntimeError:
        return None


def require_exe(name: str = "mpvw.exe") -> str:
    """Path to an executable inside the deploy directory, with both checks done."""
    path = os.path.join(deploy_dir(), name)
    if not os.path.isfile(path):
        raise RuntimeError("%r is missing from the deploy directory %r"
                           % (name, deploy_dir()))
    return path


if __name__ == "__main__":
    # `python paths.py` prints what it resolved -- the fastest way to confirm the
    # module agrees with reality before trusting 17 callers to it.
    print("repo_root      :", repo_root())
    print("workspace_root :", workspace_root())
    print("deploy_dir     :", deploy_dir_or_none() or "(not found)")
    print()
    for label, path in (("repo", repo_root()), ("workspace", workspace_root())):
        print("%-14s exists=%s" % (label, os.path.isdir(path)))
    d = deploy_dir_or_none()
    print("%-14s exists=%s  mpvw.exe=%s" % (
        "deploy", bool(d), bool(d) and os.path.isfile(os.path.join(d, "mpvw.exe"))))
