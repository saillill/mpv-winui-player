"""Deeper csharp-ls check: does it resolve symbols, or only echo the file node?

`verify_csharp_ls.py` proved the handshake and the four capabilities, but
`documentSymbol` came back with a single node -- the file itself. That is not proof the
project loaded: a server that has not indexed anything can still answer with the root
node.

This asks for things that REQUIRE a resolved symbol table:

  * `textDocument/definition` on an identifier the project defines
  * `textDocument/hover` on the same position
  * `workspace/symbol` for a name that appears in many files

If the project did not load, these return null/empty and the verdict is honest about
it rather than reporting the handshake as success.

Run:  python _work/tooling-20261027/verify_csharp_ls_deep.py
"""
from __future__ import annotations

import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from verify_csharp_ls import Lsp, REPO, TOOL, TARGET  # noqa: E402


def uri(path: str) -> str:
    return "file:///" + path.replace("\\", "/")


def position_of(text: str, needle: str, occurrence: int = 1):
    """1-based line/0-based column of the nth occurrence of `needle`."""
    idx = -1
    for _ in range(occurrence):
        idx = text.find(needle, idx + 1)
        if idx < 0:
            return None
    line = text.count("\n", 0, idx)
    col = idx - (text.rfind("\n", 0, idx) + 1)
    return line, col


def main() -> int:
    if not os.path.exists(TARGET):
        print(f"FAIL: target not found: {TARGET}")
        return 2

    text = open(TARGET, encoding="utf-8", errors="replace").read()
    print(f"file   : {os.path.relpath(TARGET, REPO)}  ({len(text)} chars)")
    print(f"server : {TOOL}\n")

    lsp = Lsp(TOOL, REPO)
    results = {}

    # Pick a symbol the file certainly defines and uses.
    probes = []
    for needle, label in (("ToCommand", "ToCommand"),
                          ("class MpvSettings", "class MpvSettings"),
                          ("BuildApplyAllCommands", "BuildApplyAllCommands")):
        pos = position_of(text, needle)
        if pos:
            probes.append((needle, label, pos))
    print("probes:")
    for needle, label, (ln, col) in probes:
        print(f"  {label:<24} line {ln} col {col}")

    try:
        init = lsp.request("initialize", {
            "processId": os.getpid(),
            "rootUri": uri(REPO),
            "capabilities": {
                "textDocument": {
                    "hover": {"contentFormat": ["markdown", "plaintext"]},
                    "definition": {"linkSupport": True},
                }
            },
            "workspaceFolders": [{"uri": uri(REPO), "name": "mpv-clean"}],
        }, timeout=120.0)
        if not init or "result" not in init:
            print("FAIL: initialize did not answer")
            return 1
        print(f"\nPASS  initialize -> {init['result'].get('serverInfo', {}).get('name')}")

        lsp.notify("initialized", {})
        lsp.notify("textDocument/didOpen", {
            "textDocument": {"uri": uri(TARGET), "languageId": "csharp",
                             "version": 1, "text": text}
        })
        # csharp-ls loads the solution lazily; give it real time on a 328-file project.
        print("      waiting 20 s for project load...")
        time.sleep(20.0)

        for needle, label, (ln, col) in probes:
            hov = lsp.request("textDocument/hover", {
                "textDocument": {"uri": uri(TARGET)},
                "position": {"line": ln, "character": col},
            }, timeout=90.0)
            h = hov.get("result") if hov else None
            ok = bool(h and h.get("contents"))
            txt = ""
            if ok:
                c = h["contents"]
                txt = c.get("value", "") if isinstance(c, dict) else str(c)
                txt = " ".join(txt.split())[:90]
            print(f"  {'PASS' if ok else 'FAIL'}  hover {label:<24} {txt}")
            results[f"hover:{label}"] = ok

            d = lsp.request("textDocument/definition", {
                "textDocument": {"uri": uri(TARGET)},
                "position": {"line": ln, "character": col},
            }, timeout=90.0)
            dr = d.get("result") if d else None
            locations = dr if isinstance(dr, list) else ([dr] if dr else [])
            ok = bool(locations)
            where = ""
            if ok:
                loc = locations[0]
                where = os.path.basename(
                    loc.get("uri", loc.get("targetUri", "")).replace("%20", " "))
            print(f"  {'PASS' if ok else 'FAIL'}  definition {label:<20} -> {where or '(none)'}")
            results[f"def:{label}"] = ok

        ws = lsp.request("workspace/symbol", {"query": "MpvSettings"}, timeout=90.0)
        wr = ws.get("result") if ws else None
        count = len(wr) if isinstance(wr, list) else 0
        print(f"  {'PASS' if count else 'FAIL'}  workspace/symbol 'MpvSettings' -> {count} hit(s)")
        results["workspace/symbol"] = count > 0

    finally:
        lsp.close()

    passed = sum(1 for v in results.values() if v)
    total = len(results)
    print("\n" + "=" * 56)
    print(f"RESULT: {passed}/{total} deep queries answered")
    if passed == 0:
        print("VERDICT: FAIL -- the server handshakes but resolves nothing; the plugin")
        print("         is installed and pointing at a server that cannot index this")
        print("         project. Treat it as NOT working.")
        return 1
    print("VERDICT: PASS -- symbol resolution works on this project")
    return 0


if __name__ == "__main__":
    sys.exit(main())
